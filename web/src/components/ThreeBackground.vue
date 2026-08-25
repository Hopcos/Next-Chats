<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import * as THREE from 'three'
import { kernel } from '@/kernel'

const canvasRef = ref<HTMLDivElement | null>(null)

let renderer: THREE.WebGLRenderer | null = null
let scene: THREE.Scene | null = null
let camera: THREE.PerspectiveCamera | null = null
let rafId = 0
let points: THREE.Points | null = null
let wire: THREE.LineSegments | null = null

const themeColor = () => {
  const theme = kernel.theme.state.theme
  if (theme === 'dawn') return new THREE.Color(0x2563eb)
  if (theme === 'midnight') return new THREE.Color(0x818cf8)
  return new THREE.Color(0x38bdf8)
}

function build() {
  const host = canvasRef.value
  if (!host || renderer) return

  scene = new THREE.Scene()
  camera = new THREE.PerspectiveCamera(70, host.clientWidth / host.clientHeight, 0.1, 2000)
  camera.position.z = 220

  renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true })
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
  renderer.setSize(host.clientWidth, host.clientHeight)
  host.appendChild(renderer.domElement)

  // 粒子星云
  const count = 2600
  const positions = new Float32Array(count * 3)
  for (let i = 0; i < count * 3; i += 3) {
    positions[i] = (Math.random() - 0.5) * 700
    positions[i + 1] = (Math.random() - 0.5) * 500
    positions[i + 2] = (Math.random() - 0.5) * 700
  }
  const geo = new THREE.BufferGeometry()
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3))
  const mat = new THREE.PointsMaterial({
    color: themeColor(),
    size: 1.4,
    transparent: true,
    opacity: 0.75,
    sizeAttenuation: true,
    blending: THREE.AdditiveBlending,
  })
  points = new THREE.Points(geo, mat)
  scene.add(points)

  // 核心线框（时空编织感）
  const wireGeo = new THREE.IcosahedronGeometry(60, 1)
  const wireMat = new THREE.LineBasicMaterial({ color: themeColor(), transparent: true, opacity: 0.22 })
  wire = new THREE.LineSegments(new THREE.WireframeGeometry(wireGeo), wireMat)
  scene.add(wire)

  const animate = () => {
    rafId = requestAnimationFrame(animate)
    const t = performance.now() / 1000
    if (points) {
      points.rotation.y = t * 0.02
      points.rotation.x = Math.sin(t * 0.01) * 0.1
    }
    if (wire) {
      wire.rotation.y = t * 0.1
      wire.rotation.x = t * 0.06
    }
    renderer?.render(scene!, camera!)
  }
  animate()
}

function onResize() {
  const host = canvasRef.value
  if (!host || !renderer || !camera) return
  camera.aspect = host.clientWidth / host.clientHeight
  camera.updateProjectionMatrix()
  renderer.setSize(host.clientWidth, host.clientHeight)
}

function dispose() {
  cancelAnimationFrame(rafId)
  points?.geometry.dispose()
  wire?.geometry.dispose()
  renderer?.dispose()
  renderer?.domElement.remove()
  renderer = null
  scene = null
  camera = null
  points = null
  wire = null
}

onMounted(build)
onUnmounted(dispose)
window.addEventListener('resize', onResize)
watch(() => kernel.theme.state.theme, build)

// 3D 开关变化：重建（启用）或销毁（禁用）
watch(
  () => kernel.settings.state.threeEnabled,
  (enabled) => {
    if (enabled && !renderer) build()
    if (!enabled) dispose()
  },
)
</script>

<template>
  <div ref="canvasRef" class="three-stage" aria-hidden="true" />
</template>
