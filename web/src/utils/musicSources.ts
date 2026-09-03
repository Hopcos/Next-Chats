/**
 * 音乐源定义（浏览器端直连第三方公开/免费音乐）。
 * 任一源不可用时，播放器按顺序自动切换到下一个源。
 * 默认源优先使用国内可达的免费高质量曲库（网易云公开歌单直链）。
 */
export interface MusicTrack {
  title: string
  artist?: string
  url: string
}

export interface MusicSource {
  name: string
  /** 拉取该源的曲目列表；可失败抛出，由播放器捕获后切换下一源 */
  fetchTracks(): Promise<MusicTrack[]>
}

/** Meting 接口返回的条目结构（子字段按需使用） */
interface MetingItem {
  name?: string
  artist?: string
  url?: string
  pic?: string
}

/** 默认网：网易云公开热歌榜（Meting 网关，返回真实可播放直链，CORS 开放） */
const METING_API = 'https://api.injahow.cn/meting/'
const NETEASE_PLAYLISTS = ['3778678', '3779629', '3778678'] // 热歌榜 / 云音乐飙升榜

export const musicSources: MusicSource[] = [
  {
    name: '网易云热歌',
    fetchTracks: async () => {
      const items = await fetchMetingList(NETEASE_PLAYLISTS[0])
      return items
        .filter((it) => it.id)
        .map((it) => ({
          title: it.name || '未知歌曲',
          artist: it.artist || '',
          url: METING_API + '?server=netease&type=url&id=' + it.id,
        }))
    },
  },
  {
    name: 'MDN 音源（jsDelivr）',
    fetchTracks: async () => [
      {
        title: "The Outfoxing (Jazz)",
        artist: 'MDN webaudio-examples',
        url: 'https://cdn.jsdelivr.net/gh/mdn/webaudio-examples@main/audio-basics/outfoxing.mp3',
      },
    ],
  },
  {
    name: 'SoundHelix',
    fetchTracks: async () =>
      Array.from({ length: 17 }, (_, i) => ({
        title: `SoundHelix Song ${i + 1}`,
        artist: 'SoundHelix (free demo)',
        url: `https://www.soundhelix.com/examples/mp3/SoundHelix-Song-${i + 1}.mp3`,
      })),
  },
  {
    name: 'W3Schools 演示音源',
    fetchTracks: async () => [
      { title: 'Horse (demo)', artist: 'W3Schools', url: 'https://www.w3schools.com/html/horse.mp3' },
    ],
  },
]

async function fetchMetingList(playlistId: string): Promise<(MetingItem & { id: string })[]> {
  const url = `${METING_API}?server=netease&type=playlist&id=${playlistId}`
  const res = await fetch(url, { signal: AbortSignal.timeout(12000) })
  if (!res.ok) throw new Error('meting http ' + res.status)
  const data = (await res.json()) as unknown[]
  const rows = Array.isArray(data) ? data : []
  return rows.map((r) => {
    const it = (r || {}) as MetingItem
    const id = String(it.url || '').match(/id=(\d+)/)?.[1] || String(it.pic || '').match(/id=(\d+)/)?.[1] || ''
    return { ...it, id }
  })
}
