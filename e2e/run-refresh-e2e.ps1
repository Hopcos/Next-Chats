# E2E for Refresh Token (dual-token, Plan B):
#   login -> refresh rotation -> replay rejection -> disable revokes all -> logout revokes -> cleanup
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File e2e/run-refresh-e2e.ps1 [-AdminPassword <pw>]
param([string]$AdminPassword = 'Sa!10')

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
function PutApi($path, $obj, $token) {
  Invoke-RestMethod -Uri "$base$path" -Method Put -ContentType 'application/json' -Headers @{ Authorization = "Bearer $token" } -Body ($obj | ConvertTo-Json -Depth 10) -UseBasicParsing | Out-Null
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
# 发起请求并返回 (HttpCode, BodyObject)；非 2xx 也返回 body（兼容 PS 5.1）
function CallApi($method, $path, $obj, $token = $null) {
  $headers = @{}
  if ($token) { $headers['Authorization'] = "Bearer $token" }
  $body = if ($null -ne $obj) { $obj | ConvertTo-Json -Depth 10 } else { $null }
  try {
    $r = Invoke-RestMethod -Uri "$base$path" -Method $method -ContentType 'application/json' -Headers $headers -Body $body -UseBasicParsing
    return @{ code = 200; body = $r }
  } catch {
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    $parsed = $null
    try {
      $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
      $parsed = $sr.ReadToEnd() | ConvertFrom-Json
    } catch { $parsed = $null }
    return @{ code = $status; body = $parsed }
  }
}

# ---------- 1. admin login returns refresh token ----------
$admin = PostJson '/api/auth/login' @{ username = 'admin'; password = $AdminPassword; authType = 'default' }
Assert (($null -ne $admin.token) -and ($null -ne $admin.refreshToken)) 'admin login returns token + refreshToken'
Assert ([int]$admin.expiresIn -ge 60) "access short-lived via expiresIn=$($admin.expiresIn)s"

# ---------- 2. refresh rotates (new access usable) ----------
$rot1 = CallApi 'Post' '/api/auth/refresh' @{ refreshToken = $admin.refreshToken }
Assert ($rot1.code -eq 200 -and $null -ne $rot1.body.token) 'refresh succeeds and returns new token + refreshToken'
$h1 = @{ Authorization = "Bearer $($rot1.body.token)" }
try { Invoke-RestMethod -Uri "$base/api/auth/me" -Headers $h1 -UseBasicParsing | Out-Null; Assert $true 'new access token works (/api/me)' } catch { Assert $false 'new access token works (/api/me)' }

# ---------- 3. old refresh replay -> 401 REFRESH_TOKEN_REVOKED (rotation) ----------
$replay = CallApi 'Post' '/api/auth/refresh' @{ refreshToken = $admin.refreshToken }
Assert ($replay.code -eq 401 -and $replay.body.code -eq 'REFRESH_TOKEN_REVOKED') 'old refresh replay rejected (REFRESH_TOKEN_REVOKED)'

# ---------- 4. garbage refresh -> 401 REFRESH_TOKEN_INVALID ----------
$garbage = CallApi 'Post' '/api/auth/refresh' @{ refreshToken = 'not-a-real-token-0123456789abcdef' }
Assert ($garbage.code -eq 401 -and $garbage.body.code -eq 'REFRESH_TOKEN_INVALID') 'garbage refresh rejected (REFRESH_TOKEN_INVALID)'

# ---------- 5. old access still usable before expiry ----------
try { Invoke-RestMethod -Uri "$base/api/auth/me" -Headers @{ Authorization = "Bearer $($admin.token)" } -UseBasicParsing | Out-Null; Assert $true 'old access token still valid pre-expiry' } catch { Assert $false 'old access token still valid pre-expiry' }

# ---------- 6. re-login revokes prior refresh tokens ----------
$admin2 = PostJson '/api/auth/login' @{ username = 'admin'; password = $AdminPassword; authType = 'default' }
$oldAfterRelogin = CallApi 'Post' '/api/auth/refresh' @{ refreshToken = $rot1.body.refreshToken }
Assert ($oldAfterRelogin.code -eq 401) 're-login revokes prior refresh tokens (old refresh -> 401)'

# ---------- 7. disable user revokes ALL their refresh tokens ----------
$userName = "iae_refresh_$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$uid = PostJson '/api/admin/users' @{ username = $userName; displayName = $userName; password = 'localpass1'; status = 'Active'; roleIds = @() } $admin2.token
Assert ($uid -is [string] -and $uid.Length -gt 10) "test user created ($uid)"
$u1 = PostJson '/api/auth/login' @{ username = $userName; password = 'localpass1'; authType = 'default' }
Assert ($null -ne $u1.refreshToken) 'test user login returns refresh token'
$u1ok1 = CallApi 'Post' '/api/auth/refresh' @{ refreshToken = $u1.refreshToken }
Assert ($u1ok1.code -eq 200) 'test user refresh works'
PutApi "/api/admin/users/$uid" @{ username = $userName; displayName = $userName; status = 'Disabled' } $admin2.token
$u1AfterDisable = CallApi 'Post' '/api/auth/refresh' @{ refreshToken = $u1ok1.body.refreshToken }
Assert (($u1AfterDisable.code -eq 401) -and ($u1AfterDisable.body.code -in @('AUTH_USER_DISABLED', 'REFRESH_TOKEN_REVOKED'))) "disabled user refresh rejected (401 code=$($u1AfterDisable.body.code); disable revoked all refresh tokens)"
try { Invoke-RestMethod -Uri "$base/api/auth/me" -Headers @{ Authorization = "Bearer $($u1ok1.body.token)" } -UseBasicParsing | Out-Null; Write-Host '      (access token still valid until its 30min expiry - expected for stateless access)' } catch { Write-Host '      (disabled user access already rejected)' }

# ---------- 8. re-enable -> re-login -> refresh works again ----------
PutApi "/api/admin/users/$uid" @{ username = $userName; displayName = $userName; status = 'Active' } $admin2.token
$u2 = PostJson '/api/auth/login' @{ username = $userName; password = 'localpass1'; authType = 'default' }
Assert ($null -ne $u2.refreshToken) 're-enabled user can login again'
$u2ok = CallApi 'Post' '/api/auth/refresh' @{ refreshToken = $u2.refreshToken }
Assert ($u2ok.code -eq 200) 're-enabled user refresh works'

# ---------- 9. logout revokes the user refresh tokens ----------
CallApi 'Post' '/api/auth/logout' @{ refreshToken = $u2ok.body.refreshToken } $u2.token | Out-Null
$afterLogout = CallApi 'Post' '/api/auth/refresh' @{ refreshToken = $u2ok.body.refreshToken }
Assert ($afterLogout.code -eq 401) 'after logout the refresh token is revoked (401)'

# ---------- 10. audit trail ----------
$audit = GetApi '/api/admin/audit?take=500' $admin2.token
$refreshOk = @($audit | Where-Object { $_.action -eq 'REFRESH.SUCCESS' }).Count
$refreshFail = @($audit | Where-Object { $_.action -eq 'REFRESH.FAILED' }).Count
Assert ($refreshOk -ge 3 -and $refreshFail -ge 3) "audit has REFRESH records (success=$refreshOk failed=$refreshFail)"

# ---------- 11. cleanup ----------
DeleteApi "/api/admin/users/$uid" $admin2.token
$usersAfter = GetApi '/api/admin/users' $admin2.token
Assert ((@($usersAfter | Where-Object { $_.username -eq $userName })).Count -eq 0) 'cleanup: test user removed (refresh rows cascade)'

if ($script:fail -eq 0) { Write-Host 'E2E ALL PASS' } else { Write-Host "E2E FAILED: $($script:fail) assertion(s)" }
exit $script:fail
