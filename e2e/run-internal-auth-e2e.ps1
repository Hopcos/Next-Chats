# E2E for Internal Auth (acs/ucs): admin CRUD -> providers -> internal login -> auto-provision -> uniqueness -> role-based catalog -> cleanup
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5210'
$script:fail = 0

function Assert($cond, $msg) {
  if ($cond) { Write-Host "PASS: $msg" }
  else { Write-Host "FAIL: $msg"; $script:fail++ }
}

function PostJson($path, $obj, $token = $null) {
  $headers = @{}
  if ($token) { $headers['Authorization'] = "Bearer $token" }
  Invoke-RestMethod -Uri "$base$path" -Method Post -ContentType 'application/json' -Headers $headers -Body ($obj | ConvertTo-Json -Depth 10) -UseBasicParsing
}
function GetApi($path, $token) {
  Invoke-RestMethod -Uri "$base$path" -Headers @{ Authorization = "Bearer $token" } -UseBasicParsing
}
function DeleteApi($path, $token) {
  try {
    Invoke-WebRequest -Uri "$base$path" -Method Delete -Headers @{ Authorization = "Bearer $token" } -UseBasicParsing | Out-Null
    Write-Host "      DEL $path OK"
  } catch {
    $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'noresp' }
    Write-Host "WARN: DEL $path -> $code (cleanup continues)"
  }
}

# ---------- 1. admin default login ----------
$admin = PostJson '/api/auth/login' @{ username = 'admin'; password = 'admin123'; authType = 'default' }
Assert ($null -ne $admin.token) 'admin default login OK'

# ---------- 2. user role id ----------
$roles = GetApi '/api/admin/roles' $admin.token
$userRole = $roles | Where-Object { $_.code -eq 'user' } | Select-Object -First 1
Assert ($null -ne $userRole) 'user role exists'

# ---------- 3. create acs provider (NotEmpty rule, top-level sessionID) ----------
$acsId = PostJson '/api/admin/internal-auth' @{
  name = 'acs'; api = 'http://127.0.0.1:53131/login'; httpMethod = 'POST'; requestFormat = 'BodyJson'
  usernameField = 'username'; passwordField = 'password'; enabled = $true; timeoutSeconds = 10
  successRules = @(@{ field = 'sessionID'; operator = 'NotEmpty' }); defaultRoleIds = @($userRole.id)
} $admin.token
Assert ($acsId -is [string] -and $acsId.Length -gt 10) "acs provider created ($acsId)"

# ---------- 4. anonymous providers endpoint ----------
$provs = Invoke-RestMethod -Uri "$base/api/auth/providers" -UseBasicParsing
Assert ((@($provs | Where-Object { $_.name -eq 'acs' })).Count -eq 1) 'providers endpoint lists acs'

# ---------- 5. internal login success -> auto-provision ----------
try {
  $login1 = PostJson '/api/auth/login' @{ username = 'iae_unit'; password = 'secret123'; authType = 'acs' }
  Assert ($null -ne $login1.token) 'internal login returns token'
  Assert ($login1.user.displayName -eq 'iae_unit') 'displayName == username'
  Assert ($login1.user.authType -eq 'acs') 'authType == acs'
  Assert (@($login1.user.roles) -contains 'user') 'roles contain user'
} catch {
  $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'noresp' }
  Assert $false "internal login failed ($code)"
}

# ---------- 6. admin user list shows authType=acs ----------
$users = GetApi '/api/admin/users' $admin.token
$u = $users | Where-Object { $_.username -eq 'iae_unit' } | Select-Object -First 1
Assert ($null -ne $u -and $u.authType -eq 'acs' -and $u.status -eq 'Active') 'admin user list shows iae_unit authType=acs status=Active'

# ---------- 7. re-login does not duplicate ----------
$login2 = PostJson '/api/auth/login' @{ username = 'iae_unit'; password = 'secret123'; authType = 'acs' }
$users2 = GetApi '/api/admin/users' $admin.token
$cnt2 = @($users2 | Where-Object { $_.username -eq 'iae_unit' }).Count
Assert ($cnt2 -eq 1) "re-login no duplicate user (count=$cnt2)"
Assert ($login2.user.id -eq $login1.user.id) 're-login returns same user id'

# ---------- 8. wrong password -> 401 ----------
try {
  PostJson '/api/auth/login' @{ username = 'iae_unit'; password = 'wrong'; authType = 'acs' } | Out-Null
  Assert $false 'wrong password rejected'
} catch {
  Assert (($_.Exception.Response -ne $null) -and ([int]$_.Exception.Response.StatusCode -eq 401)) 'wrong password -> 401'
}

# ---------- 9. unknown provider (ucs not configured) -> 401 ----------
try {
  PostJson '/api/auth/login' @{ username = 'x'; password = 'x'; authType = 'ucs' } | Out-Null
  Assert $false 'unknown provider rejected'
} catch {
  Assert (($_.Exception.Response -ne $null) -and ([int]$_.Exception.Response.StatusCode -eq 401)) 'unknown provider -> 401'
}

# ---------- 10. non-JSON success response -> 401 (invalid credentials) ----------
$badJsonId = PostJson '/api/admin/internal-auth' @{
  name = 'badjson'; api = 'http://127.0.0.1:53131/login-badjson'; httpMethod = 'POST'; requestFormat = 'BodyJson'
  usernameField = 'username'; passwordField = 'password'; enabled = $true; timeoutSeconds = 10
  successRules = @(@{ field = 'sessionID'; operator = 'NotEmpty' }); defaultRoleIds = @()
} $admin.token
try {
  PostJson '/api/auth/login' @{ username = 'iae_unit'; password = 'secret123'; authType = 'badjson' } | Out-Null
  Assert $false 'badjson response rejected'
} catch {
  Assert (($_.Exception.Response -ne $null) -and ([int]$_.Exception.Response.StatusCode -eq 401)) 'non-JSON response -> 401'
}
DeleteApi "/api/admin/internal-auth/$badJsonId" $admin.token

# ---------- 11. internal user /api/me + role-based catalog ----------
$me = Invoke-RestMethod -Uri "$base/api/me" -Headers @{ Authorization = "Bearer $($login1.token)" } -UseBasicParsing
Assert ($me.authType -eq 'acs' -and $me.username -eq 'iae_unit') 'internal user /api/me authType=acs'
$catalog = Invoke-RestMethod -Uri "$base/api/me/catalog" -Headers @{ Authorization = "Bearer $($login1.token)" } -UseBasicParsing
$adminCatalog = Invoke-RestMethod -Uri "$base/api/me/catalog" -Headers @{ Authorization = "Bearer $($admin.token)" } -UseBasicParsing
# user role bound modelIds from roles list (includeBindings)
$modelIds = @($userRole.modelIds)
if ($modelIds.Count -gt 0) {
  $within = $true
  foreach ($p in $catalog.providers) { foreach ($m in $p.models) { if ($modelIds -notcontains $m.id) { $within = $false } } }
  Assert $within 'catalog models are within user role binding'
  Assert ($catalog.providers.Count -lt @($adminCatalog.providers).Count) 'role-based catalog narrower than admin'
  Write-Host "      (user sees $($catalog.providers.Count) provider(s), admin sees $(@($adminCatalog.providers).Count))"
} else {
  Write-Host '      (user role has no model binding; skip model filtering assertion)'
}

# ---------- 12. (AuthType, Username) composite uniqueness: default + acs can share username ----------
$sameId = PostJson '/api/admin/users' @{ username = 'iae_same'; password = 'localtest1'; roleIds = @($userRole.id) } $admin.token
$loginSameAcs = PostJson '/api/auth/login' @{ username = 'iae_same'; password = 'secret123'; authType = 'acs' }
Assert ($null -ne $loginSameAcs.token) 'acs login with same username as a default user succeeds'
$loginSameDefault = PostJson '/api/auth/login' @{ username = 'iae_same'; password = 'localtest1'; authType = 'default' }
Assert ($null -ne $loginSameDefault.token) 'default login with same username succeeds'
try {
  PostJson '/api/auth/login' @{ username = 'iae_same'; password = 'secret123'; authType = 'default' } | Out-Null
  Assert $false 'default login with internal password rejected'
} catch {
  Assert ([int]$_.Exception.Response.StatusCode -eq 401) 'default login with acs-only password -> 401'
}
$users4 = GetApi '/api/admin/users' $admin.token
$sameUsers = @($users4 | Where-Object { $_.username -eq 'iae_same' })
Assert ($sameUsers.Count -eq 2) "default + acs users coexist (count=$($sameUsers.Count))"
$sameDefault = @($sameUsers | Where-Object { $_.authType -eq 'default' })[0]
$sameAcs = @($sameUsers | Where-Object { $_.authType -eq 'acs' })[0]
Assert (($null -ne $sameDefault) -and ($null -ne $sameAcs)) 'both authType variants exist'

# ---------- 13. Equals rule + dotted path + GET provider ----------
$ucsId = PostJson '/api/admin/internal-auth' @{
  name = 'ucs'; api = 'http://127.0.0.1:53131/login-check'; httpMethod = 'GET'; requestFormat = 'BodyJson'
  usernameField = 'username'; passwordField = 'password'; enabled = $true; timeoutSeconds = 10
  successRules = @(@{ field = 'data.sessionID'; operator = 'Equals'; expectedValue = 'S-abc' }); defaultRoleIds = @($userRole.id)
} $admin.token
$login3 = PostJson '/api/auth/login' @{ username = 'iae_ucs'; password = 'whatever'; authType = 'ucs' }
Assert ($null -ne $login3.token) 'ucs login (GET + Equals dotted rule) success'
$users5 = GetApi '/api/admin/users' $admin.token
Assert ((@($users5 | Where-Object { $_.username -eq 'iae_ucs' })).Count -eq 1) 'ucs user auto-created once'

# ---------- 14. cleanup (plain loops, no Select-Object -First pipeline quirks) ----------
$allUsers = GetApi '/api/admin/users' $admin.token
foreach ($name in @('iae_unit', 'iae_ucs', 'iae_same')) {
  foreach ($uu in $allUsers) {
    if ($uu.username -eq $name) { DeleteApi "/api/admin/users/$($uu.id)" $admin.token }
  }
}
$allProvs = Invoke-RestMethod -Uri "$base/api/admin/internal-auth" -Headers @{ Authorization = "Bearer $($admin.token)" } -UseBasicParsing
foreach ($pp in $allProvs) { DeleteApi "/api/admin/internal-auth/$($pp.id)" $admin.token }
$usersAfter = GetApi '/api/admin/users' $admin.token
Assert ((@($usersAfter | Where-Object { $_.username -like 'iae_*' })).Count -eq 0) 'cleanup: no iae_ users left'
$provsAfter = Invoke-RestMethod -Uri "$base/api/auth/providers" -UseBasicParsing
Assert ((@($provsAfter | Where-Object { $_.name -in @('acs', 'ucs', 'badjson') })).Count -eq 0) 'cleanup: no test providers left'

# ---------- summary ----------
if ($script:fail -eq 0) { Write-Host 'E2E ALL PASS' } else { Write-Host "E2E FAILED: $($script:fail) assertion(s)" }
exit $script:fail
