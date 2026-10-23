<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';
import L from 'leaflet';
import { useJobsStore } from '../stores/jobs';
import { useAuthStore } from '../stores/auth';
import { useVenuesStore } from '../stores/venues';
import type { JobResponseDto } from '../types';
import { SERVICE_CATALOG } from '../types';

const emit = defineEmits<{
  (e: 'marker-click', job: JobResponseDto): void;
}>();

const jobsStore = useJobsStore();
const authStore = useAuthStore();
const venuesStore = useVenuesStore();

const mapContainer = ref<HTMLElement | null>(null);
let map: L.Map | null = null;
let markers: L.Marker[] = [];
let draftMarker: L.Marker | null = null;

const mapType = ref<'roadmap' | 'satellite'>('roadmap');
let roadmapLayer: L.TileLayer | null = null;
let satelliteLayer: L.TileLayer | null = null;

function setMapType(type: 'roadmap' | 'satellite') {
  if (!map || mapType.value === type) return;
  if (type === 'satellite') {
    if (roadmapLayer && map.hasLayer(roadmapLayer)) map.removeLayer(roadmapLayer);
    if (satelliteLayer) satelliteLayer.addTo(map);
  } else {
    if (satelliteLayer && map.hasLayer(satelliteLayer)) map.removeLayer(satelliteLayer);
    if (roadmapLayer) roadmapLayer.addTo(map);
  }
  mapType.value = type;
}

onMounted(() => {
  initMap();
  venuesStore.loadVenues();
});

onUnmounted(() => {
  if (map) {
    map.remove();
    map = null;
  }
});

function initMap() {
  if (!mapContainer.value) return;

  // Center on Hanoi by default
  map = L.map(mapContainer.value, { 
    zoomControl: false,
    attributionControl: false
  }).setView([21.015, 105.815], 13);

  // Google Maps Roadmap Layer with POIs/Transit/Business icons hidden (Clean Alobo Style)
  roadmapLayer = L.tileLayer('https://mt1.google.com/vt/lyrs=m&hl=vi&x={x}&y={y}&z={z}&apistyle=s.t:33|p.v:off,s.t:6|p.v:off,s.t:2|p.v:off', {
    maxZoom: 20,
    subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
    attribution: 'Google Maps'
  });

  // Google Maps Hybrid Satellite Layer (Satellite imagery + road labels, POIs stripped)
  satelliteLayer = L.tileLayer('https://mt1.google.com/vt/lyrs=y&hl=vi&x={x}&y={y}&z={z}&apistyle=s.t:6|p.v:off,s.t:33|p.v:off,s.t:2|p.v:off', {
    maxZoom: 20,
    subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
    attribution: 'Google Maps'
  });

  roadmapLayer.addTo(map);

  map.on('click', handleMapClick);
  map.on('zoomend', () => renderPins());

  renderPins();
}

let venueEditMarker: L.Marker | null = null;

// Watch venue editing mode to show/move editing marker on main map
watch(
  () => [venuesStore.isEditingVenueMode, venuesStore.editingVenueData?.latitude, venuesStore.editingVenueData?.longitude],
  ([isEditing, lat, lng]) => {
    if (!map) return;
    if (isEditing && lat != null && lng != null) {
      const numLat = Number(lat);
      const numLng = Number(lng);
      if (!venueEditMarker) {
        const editIcon = L.divIcon({
          className: 'custom-pick-marker',
          html: `
            <div class="relative flex flex-col items-center select-none pointer-events-none" style="transform: translate(-50%, -100%);">
              <div class="w-10 h-10 rounded-full bg-gradient-to-br from-indigo-600 to-sky-500 text-white flex items-center justify-center text-lg shadow-2xl ring-4 ring-white animate-bounce">
                🏛️
              </div>
              <div class="w-3 h-3 bg-indigo-600 rounded-full mt-0.5 animate-ping opacity-75"></div>
            </div>
          `,
          iconSize: [0, 0],
          iconAnchor: [0, 0]
        });
        venueEditMarker = L.marker([numLat, numLng], { icon: editIcon }).addTo(map);
        map.flyTo([numLat, numLng], 16, { animate: true, duration: 0.8 });
      } else {
        venueEditMarker.setLatLng([numLat, numLng]);
      }
    } else {
      if (venueEditMarker) {
        map.removeLayer(venueEditMarker);
        venueEditMarker = null;
      }
    }
  },
  { deep: true }
);

// Watch visible jobs and filter criteria to immediately re-render pins
watch(
  [
    () => jobsStore.selectedService,
    () => jobsStore.visibleJobs,
    () => jobsStore.jobs,
    () => jobsStore.selectedDate,
    () => jobsStore.selectedMonth,
    () => jobsStore.viewAllMonth,
    () => venuesStore.venues
  ],
  () => {
    renderPins();
  },
  { deep: true }
);

// Watch drawer closing to remove draft marker if any
watch(() => jobsStore.drawerOpen, (isOpen) => {
  if (!isOpen && draftMarker && map) {
    map.removeLayer(draftMarker);
    draftMarker = null;
  }
});

function getServiceIcon(code: string) {
  const list = authStore.services.length > 0 ? authStore.services : SERVICE_CATALOG;
  return list.find(s => s.code === code)?.icon || '🏛️';
}

function getAloboPinContent(serviceCode?: string, jobCount = 0): string {
  if (jobCount === 0 || !serviceCode) return '🏛️';
  const code = serviceCode.trim().toUpperCase();
  if (code === 'BUTTERFLY') return '🦋';
  if (code === 'FPV') return '🚁';
  if (code === 'FLYCAM') return '✈️';
  if (code === 'CAMERA') return '🎥';
  if (code === 'PHOTO') return '📸';
  return getServiceIcon(serviceCode);
}

interface VenueMapItem {
  venueId: number | null;
  name: string;
  subAddress: string;
  lat: number;
  lng: number;
  jobs: JobResponseDto[];
  hasOwner: boolean;
}

function renderPins() {
  if (!map) return;

  // Clear existing markers
  markers.forEach(m => map!.removeLayer(m));
  markers = [];

  const currentUserId = authStore.currentUser?.id;

  // 1. Build unified list of venue items
  const venueItems: VenueMapItem[] = [];

  // A. Add from SavedVenues
  venuesStore.venues.forEach(v => {
    const matchingJobs = jobsStore.visibleJobs.filter(j => {
      const dLat = Math.abs(j.latitude - v.latitude);
      const dLng = Math.abs(j.longitude - v.longitude);
      return (dLat < 0.0008 && dLng < 0.0008) || 
             (j.locationName && j.locationName.trim().toLowerCase() === v.name.trim().toLowerCase());
    });

    matchingJobs.sort((a, b) => a.startTime.localeCompare(b.startTime));
    const hasOwner = matchingJobs.some(j => j.ownerUserId === currentUserId);

    venueItems.push({
      venueId: v.id,
      name: v.name,
      subAddress: v.subAddress || '',
      lat: v.latitude,
      lng: v.longitude,
      jobs: matchingJobs,
      hasOwner
    });
  });

  // B. Add any visibleJobs whose coordinates didn't match a saved venue yet
  jobsStore.visibleJobs.forEach(job => {
    const matched = venueItems.some(v => {
      const dLat = Math.abs(v.lat - job.latitude);
      const dLng = Math.abs(v.lng - job.longitude);
      return (dLat < 0.0008 && dLng < 0.0008) || 
             (job.locationName && v.name.trim().toLowerCase() === job.locationName.trim().toLowerCase());
    });

    if (!matched) {
      const isOwner = job.ownerUserId === currentUserId;
      const existingNearby = venueItems.find(v => {
        const dLat = Math.abs(v.lat - job.latitude);
        const dLng = Math.abs(v.lng - job.longitude);
        return dLat < 0.0008 && dLng < 0.0008;
      });

      if (existingNearby) {
        existingNearby.jobs.push(job);
        existingNearby.jobs.sort((a, b) => a.startTime.localeCompare(b.startTime));
        if (isOwner) existingNearby.hasOwner = true;
      } else {
        venueItems.push({
          venueId: null,
          name: job.locationName || 'Địa điểm tiệc chưa đặt tên',
          subAddress: job.subAddress || '',
          lat: job.latitude,
          lng: job.longitude,
          jobs: [job],
          hasOwner: isOwner
        });
      }
    }
  });

function getDominantServiceCode(jobs: JobResponseDto[]): string | undefined {
  if (!jobs || jobs.length === 0) return undefined;
  const counts: Record<string, number> = {};
  jobs.forEach(j => {
    const code = (j.serviceCode || 'OTHER').toUpperCase();
    counts[code] = (counts[code] || 0) + 1;
  });
  let maxCode: string | undefined = undefined;
  let maxVal = -1;
  for (const [code, val] of Object.entries(counts)) {
    if (val > maxVal) {
      maxVal = val;
      maxCode = code;
    }
  }
  return maxCode;
}

function getServiceBreakdownBadges(jobs: JobResponseDto[]): string {
  if (!jobs || jobs.length === 0) return '';
  const counts: Record<string, number> = {};
  jobs.forEach(j => {
    const code = (j.serviceCode || 'OTHER').toUpperCase();
    counts[code] = (counts[code] || 0) + 1;
  });
  return Object.entries(counts)
    .sort((a, b) => b[1] - a[1])
    .map(([code, count]) => `<span>${count}${getServiceIcon(code)}</span>`)
    .join(' ');
}

function renderSingleVenuePin(item: VenueMapItem) {
  if (!map) return;
  const jobCount = item.jobs.length;
  const isOwner = item.hasOwner;

  // Dominant service code determines the pin vector icon
  const dominantService = getDominantServiceCode(item.jobs);
  const displayContent = getAloboPinContent(dominantService, jobCount);

  // Pin theme class
  let pinClass = 'alobo-pin-empty';
  if (isOwner) {
    pinClass = 'alobo-pin-owner';
  } else if (jobCount > 0) {
    pinClass = 'alobo-pin-active';
  }

  // Service breakdown badge (e.g. 2🦋 1🚁)
  const serviceBadges = getServiceBreakdownBadges(item.jobs);

  // Alobo pin with hover-only tooltip and horizontal un-rotated service badge
  const html = `
    <div class="alobo-pin-container">
      <div class="alobo-venue-tooltip">
        ${item.name}
      </div>
      ${jobCount > 0 ? `<div class="alobo-pin-badge">${serviceBadges}</div>` : ''}
      <div class="alobo-pin-icon ${pinClass}">
        <span class="alobo-pin-icon-content">${displayContent}</span>
      </div>
    </div>
  `;

  const icon = L.divIcon({
    className: 'alobo-marker-wrapper',
    html,
    iconSize: [0, 0],
    iconAnchor: [0, 0]
  });

  const marker = L.marker([item.lat, item.lng], { icon }).addTo(map);

  // Build Interactive Popup
  const filterPeriod = jobsStore.viewAllMonth 
    ? `Tháng ${jobsStore.selectedMonth}` 
    : `Ngày ${jobsStore.selectedDate}`;

  const popupDiv = document.createElement('div');
  popupDiv.className = 'p-3 min-w-[270px] max-w-[320px] text-xs font-sans select-none';

  popupDiv.innerHTML = `
    <div class="space-y-2.5">
      <!-- Header with Venue Name & Rename toggle -->
      <div class="border-b border-slate-100 pb-2">
        <div class="flex items-center justify-between gap-1 mb-1">
          <span class="text-[10px] font-black uppercase text-indigo-600 tracking-wider flex items-center gap-1.5">
            <span>🏛️</span> Địa Điểm Tiệc
          </span>
          <button type="button" class="btn-toggle-rename text-[11px] font-bold text-sky-600 hover:text-sky-800 flex items-center gap-1 cursor-pointer">
            ✏️ Đổi tên
          </button>
        </div>

        <!-- Display Box -->
        <div class="venue-display-box">
          <h3 class="font-black text-sm text-slate-900 leading-snug venue-title-text">${item.name}</h3>
          <p class="text-[11px] text-slate-500 mt-0.5 truncate">${item.subAddress || 'Chưa có địa chỉ chi tiết'}</p>
        </div>

        <!-- Inline Rename Form (hidden by default) -->
        <div class="venue-rename-box hidden mt-1.5 space-y-1.5">
          <input type="text" value="${item.name}" class="input-venue-name w-full bg-slate-50 border border-sky-300 rounded-lg px-2.5 py-1.5 font-bold text-slate-800 text-xs focus:outline-none focus:ring-2 focus:ring-sky-500/30" placeholder="Nhập tên hợp lý..." />
          <div class="flex items-center justify-end gap-1.5">
            <button type="button" class="btn-cancel-rename px-2 py-1 rounded-md bg-slate-100 hover:bg-slate-200 text-slate-600 font-semibold text-[10px] cursor-pointer">Hủy</button>
            <button type="button" class="btn-save-rename px-3 py-1 rounded-md bg-sky-600 hover:bg-sky-700 text-white font-bold text-[10px] shadow-2xs cursor-pointer">💾 Lưu tên</button>
          </div>
        </div>
      </div>

      <!-- Jobs List in current filter -->
      <div>
        <div class="flex items-center justify-between text-[11px] font-bold text-slate-500 mb-1.5">
          <span>Lịch job (${filterPeriod}):</span>
          <span class="px-2 py-0.5 rounded-full ${jobCount > 0 ? 'bg-indigo-50 text-indigo-700 font-black' : 'bg-slate-100 text-slate-500 font-semibold'} text-[10px]">
            ${serviceBadges || `${jobCount} job`}
          </span>
        </div>

        ${jobCount > 0 ? `
          <div class="space-y-1.5 max-h-[170px] overflow-y-auto pr-0.5">
            ${item.jobs.map(j => {
              const crewText = j.assignments.map(a => a.userName).filter(Boolean).join(', ');
              const displayCrew = crewText ? `Thợ: ${crewText}` : (j.externalWorkersNote ? `Thợ: ${j.externalWorkersNote}` : 'Chưa gán thợ');
              return `
                <div class="job-item-card p-2 rounded-xl border border-slate-200 hover:border-indigo-400 hover:bg-indigo-50/60 cursor-pointer transition flex items-center justify-between gap-1.5 group" data-job-id="${j.id}">
                  <div class="flex items-center gap-2 truncate">
                    <span class="text-base shrink-0">${getServiceIcon(j.serviceCode)}</span>
                    <div class="truncate">
                      <div class="font-bold text-slate-900 text-xs">${j.startTime} - ${j.endTime}</div>
                      <div class="text-[10px] text-slate-500 truncate">${displayCrew}</div>
                    </div>
                  </div>
                  <span class="text-indigo-600 text-[10px] font-bold group-hover:translate-x-0.5 transition shrink-0">Chi tiết ➔</span>
                </div>
              `;
            }).join('')}
          </div>
        ` : `
          <div class="text-center py-2.5 bg-slate-50 rounded-xl border border-dashed border-slate-200 text-slate-400 text-[11px]">
            Chưa có job nào trong khoảng thời gian này
          </div>
        `}
      </div>

      <!-- Button: Create New Job at this Venue -->
      <button type="button" class="btn-create-job-here w-full py-2 rounded-xl bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs flex items-center justify-center gap-1.5 shadow-sm transition active:scale-98 cursor-pointer">
        <span>➕</span>
        <span>Tạo job mới tại địa điểm này</span>
      </button>
    </div>
  `;

  // Attach DOM Events to Popup elements
  const btnToggleRename = popupDiv.querySelector('.btn-toggle-rename') as HTMLButtonElement | null;
  const btnCancelRename = popupDiv.querySelector('.btn-cancel-rename') as HTMLButtonElement | null;
  const btnSaveRename = popupDiv.querySelector('.btn-save-rename') as HTMLButtonElement | null;
  const displayBox = popupDiv.querySelector('.venue-display-box') as HTMLElement | null;
  const renameBox = popupDiv.querySelector('.venue-rename-box') as HTMLElement | null;
  const titleText = popupDiv.querySelector('.venue-title-text') as HTMLElement | null;
  const inputName = popupDiv.querySelector('.input-venue-name') as HTMLInputElement | null;

  if (btnToggleRename && displayBox && renameBox && inputName) {
    btnToggleRename.addEventListener('click', (e) => {
      L.DomEvent.stopPropagation(e);
      displayBox.classList.add('hidden');
      renameBox.classList.remove('hidden');
      inputName.focus();
      inputName.select();
    });
  }

  if (btnCancelRename && displayBox && renameBox) {
    btnCancelRename.addEventListener('click', (e) => {
      L.DomEvent.stopPropagation(e);
      renameBox.classList.add('hidden');
      displayBox.classList.remove('hidden');
    });
  }

  if (btnSaveRename && inputName && displayBox && renameBox && titleText) {
    btnSaveRename.addEventListener('click', async (e) => {
      L.DomEvent.stopPropagation(e);
      const newName = inputName.value.trim();
      if (!newName) {
        alert('Tên địa điểm không được để trống');
        return;
      }

      btnSaveRename.disabled = true;
      btnSaveRename.textContent = 'Đang lưu...';

      try {
        if (item.venueId) {
          await venuesStore.updateVenue(item.venueId, {
            name: newName,
            subAddress: item.subAddress,
            latitude: item.lat,
            longitude: item.lng
          });
        } else {
          const saved = await venuesStore.saveVenue({
            name: newName,
            subAddress: item.subAddress,
            latitude: item.lat,
            longitude: item.lng
          });
          item.venueId = saved.id;
        }

        item.name = newName;
        titleText.textContent = newName;
        renameBox.classList.add('hidden');
        displayBox.classList.remove('hidden');

        // Reload jobs and re-render pins to update the marker title on map
        await jobsStore.loadJobs();
        renderPins();
      } catch (err: any) {
        alert('Lỗi cập nhật tên địa điểm: ' + (err.message || 'Thử lại'));
      } finally {
        btnSaveRename.disabled = false;
        btnSaveRename.textContent = '💾 Lưu tên';
      }
    });
  }

  // Job item click -> Open edit drawer
  popupDiv.querySelectorAll('.job-item-card').forEach(cardEl => {
    cardEl.addEventListener('click', (e) => {
      L.DomEvent.stopPropagation(e);
      const jobId = Number(cardEl.getAttribute('data-job-id'));
      const targetJob = item.jobs.find(x => x.id === jobId);
      if (targetJob) {
        map!.closePopup();
        jobsStore.openEdit(targetJob);
        emit('marker-click', targetJob);
      }
    });
  });

  // Create job here click -> Open create drawer pre-filled
  const btnCreateHere = popupDiv.querySelector('.btn-create-job-here') as HTMLButtonElement | null;
  if (btnCreateHere) {
    btnCreateHere.addEventListener('click', (e) => {
      L.DomEvent.stopPropagation(e);
      map!.closePopup();
      if (draftMarker && map) {
        map.removeLayer(draftMarker);
        draftMarker = null;
      }
      jobsStore.openCreate(item.lat, item.lng, item.name, item.subAddress || '');
    });
  }

  marker.bindPopup(popupDiv, {
    offset: [0, -36],
    closeButton: false,
    autoPan: true
  });

  markers.push(marker);
}

// 2. Render all pins, with cluster grouping when zoomed out (currentZoom <= 12)
const currentZoom = map ? map.getZoom() : 13;

if (currentZoom <= 12 && venueItems.length > 1) {
  // Cluster nearby venues together
  const clusters: Array<{
    lat: number;
    lng: number;
    items: VenueMapItem[];
  }> = [];

  venueItems.forEach(item => {
    const matchedCluster = clusters.find(c => Math.hypot(c.lat - item.lat, c.lng - item.lng) < 0.04);
    if (matchedCluster) {
      matchedCluster.items.push(item);
      matchedCluster.lat = matchedCluster.items.reduce((sum, v) => sum + v.lat, 0) / matchedCluster.items.length;
      matchedCluster.lng = matchedCluster.items.reduce((sum, v) => sum + v.lng, 0) / matchedCluster.items.length;
    } else {
      clusters.push({
        lat: item.lat,
        lng: item.lng,
        items: [item]
      });
    }
  });

  clusters.forEach(c => {
    if (c.items.length === 1) {
      renderSingleVenuePin(c.items[0]);
    } else {
      const allNames = c.items.map(v => v.name).join(', ');
      const clusterCounts: Record<string, number> = {};
      let totalClusterJobs = 0;
      c.items.forEach(v => {
        v.jobs.forEach(j => {
          const code = (j.serviceCode || 'OTHER').toUpperCase();
          clusterCounts[code] = (clusterCounts[code] || 0) + 1;
          totalClusterJobs++;
        });
      });

      const clusterBadges = totalClusterJobs > 0 
        ? Object.entries(clusterCounts)
            .sort((a, b) => b[1] - a[1])
            .map(([code, count]) => `<span class="cluster-badge-chip">${count}${getServiceIcon(code)}</span>`)
            .join(' ')
        : `<span class="cluster-badge-chip">${c.items.length}🏛️</span>`;

      const clusterHtml = `
        <div class="alobo-cluster-marker">
          <div class="alobo-cluster-pill" title="${allNames}">
            ${clusterBadges}
          </div>
        </div>
      `;

      const clusterIcon = L.divIcon({
        className: 'alobo-cluster-wrapper',
        html: clusterHtml,
        iconSize: [0, 0],
        iconAnchor: [0, 0]
      });

      const marker = L.marker([c.lat, c.lng], { icon: clusterIcon }).addTo(map!);
      marker.on('click', () => {
        map!.setView([c.lat, c.lng], 14, { animate: true });
      });
      markers.push(marker);
    }
  });
} else {
  // Render individual venue pins
  venueItems.forEach(item => renderSingleVenuePin(item));
}
}

interface ResolvedPlace {
  location: string;
  subAddress: string;
  nearbyPois: string[];
  poiCoordinates?: Record<string, { lat: number; lng: number }>;
  snappedLat?: number;
  snappedLng?: number;
}

// In-memory geocode cache to make repeated or nearby clicks instant
const geocodeCache = new Map<string, ResolvedPlace>();

function calculateDistanceMeters(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371000;
  const dLat = (lat2 - lat1) * Math.PI / 180;
  const dLon = (lon2 - lon1) * Math.PI / 180;
  const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) + 
            Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) * 
            Math.sin(dLon / 2) * Math.sin(dLon / 2);
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

const VENUE_KEYWORDS = [
  'bệnh viện', 'viện', 'phòng khám', 'trạm y tế', 'hospital', 'clinic',
  'trường', 'đại học', 'học viện', 'cao đẳng', 'tiểu học', 'thcs', 'thpt', 'mầm non', 'school', 'university',
  'trung tâm', 'hội nghị', 'tiệc cưới', 'convention', 'palace', 'almaz', 'trống đồng', 'mipec', 'sapphire', 'cung văn hóa', 'nhà hát',
  'sân vận động', 'nhà thi đấu', 'sân bóng', 'stadium', 'arena', 'thể thao',
  'khách sạn', 'hotel', 'resort', 'homestay', 'marriott', 'intercontinental', 'sheraton', 'hilton', 'daewoo', 'lotte', 'pan pacific', 'novotel',
  'tòa nhà', 'building', 'tower', 'plaza', 'vincom', 'aeon', 'keangnam', 'landmark',
  'chùa', 'nhà thờ', 'đền', 'đình', 'công viên', 'bảo tàng', 'ubnd', 'ủy ban', 'bến xe', 'sân bay'
];

// Fast Reverse Geocoding with Google Maps-style POI Snapping:
// 1. Saved Venues Catalog (0ms, 100% accurate) -> Nearby Known Job -> Goong API -> Smart POI Ranking
async function resolvePlaceDetails(lat: number, lng: number): Promise<ResolvedPlace> {
  const cacheKey = `${lat.toFixed(4)},${lng.toFixed(4)}`;
  if (geocodeCache.has(cacheKey)) {
    return geocodeCache.get(cacheKey)!;
  }

  // 1. HIGHEST PRIORITY (0ms, 100% accurate): Check if user clicked near any SAVED VENUE
  const savedVenue = venuesStore.findNearbyVenue(lat, lng, 180);
  if (savedVenue) {
    const res: ResolvedPlace = {
      location: savedVenue.name,
      subAddress: savedVenue.subAddress || '',
      nearbyPois: [savedVenue.name],
      snappedLat: savedVenue.latitude,
      snappedLng: savedVenue.longitude
    };
    geocodeCache.set(cacheKey, res);
    return res;
  }

  // 2. Proximity check with known jobs in memory (0ms)
  const nearbyJob = jobsStore.jobs.find(j => {
    const dLat = Math.abs(j.latitude - lat);
    const dLng = Math.abs(j.longitude - lng);
    return dLat < 0.0008 && dLng < 0.0008; // ~80m radius
  });
  if (nearbyJob) {
    const res: ResolvedPlace = {
      location: nearbyJob.locationName,
      subAddress: nearbyJob.subAddress,
      nearbyPois: [nearbyJob.locationName],
      snappedLat: nearbyJob.latitude,
      snappedLng: nearbyJob.longitude
    };
    geocodeCache.set(cacheKey, res);
    return res;
  }

  // 2. Goong Reverse Geocode API (if VITE_GOONG_API_KEY is configured)
  const goongKey = (import.meta.env.VITE_GOONG_API_KEY as string)?.trim();
  if (goongKey) {
    try {
      const res = await fetch(`https://rsapi.goong.io/Geocode?latlng=${lat},${lng}&api_key=${goongKey}`);
      if (res.ok) {
        const data = await res.json();
        if (data && data.results?.length > 0) {
          const first = data.results[0];
          const name = first.name || first.address_components?.[0]?.long_name || first.formatted_address;
          const sub = first.formatted_address || '';
          const pois: string[] = data.results
            .map((r: any) => r.name)
            .filter((n: any) => Boolean(n) && n !== name)
            .slice(0, 4);
          const gLat = first.geometry?.location?.lat;
          const gLng = first.geometry?.location?.lng;
          const result: ResolvedPlace = { 
            location: name, 
            subAddress: sub, 
            nearbyPois: [name, ...pois],
            snappedLat: gLat,
            snappedLng: gLng
          };
          geocodeCache.set(cacheKey, result);
          return result;
        }
      }
    } catch { }
  }

  let location = 'Địa điểm mới';
  let subAddress = 'Hà Nội';
  let pois: string[] = [];

  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 1600);

  // 3. Smart POI-First Nominatim & Photon queries
  const fetchNominatim = async () => {
    try {
      const res = await fetch(
        `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&zoom=18&addressdetails=1`,
        {
          signal: controller.signal,
          headers: { 'Accept-Language': 'vi', 'User-Agent': 'EventMapVN/1.0' }
        }
      );
      if (!res.ok) return null;
      const data = await res.json();
      if (data && data.address) {
        const addr = data.address;
        
        // Priority: POI/Venue names (Amenity, Building, Hotel, Restaurant, Tourism, Leisure)
        const venuePOI = 
          addr.amenity || 
          addr.building || 
          addr.hotel || 
          addr.tourism || 
          addr.leisure || 
          addr.shop || 
          addr.commercial || 
          addr.restaurant;

        const isRoad = data.class === 'highway' || data.addresstype === 'road' || 
          (data.name && (data.name.startsWith('Đường') || data.name.startsWith('Phố') || data.name.startsWith('Ngõ') || data.name.startsWith('Ngách') || data.name.startsWith('Hẻm')));
        
        const cleanPoi = (!isRoad && data.name) ? data.name : venuePOI;

        const streetParts = [
          addr.house_number ? `${addr.house_number} ${addr.road || ''}` : addr.road,
          addr.suburb || addr.quarter,
          addr.city_district || addr.district || addr.county,
          addr.city || addr.state
        ].filter(Boolean);
        const street = streetParts.join(', ');

        const nomLat = data.lat ? parseFloat(data.lat) : undefined;
        const nomLng = data.lon ? parseFloat(data.lon) : undefined;

        return { 
          poiName: cleanPoi || null, 
          street,
          rawName: data.name || streetParts[0] || null,
          coords: (nomLat && nomLng) ? [nomLat, nomLng] as [number, number] : undefined
        };
      }
    } catch { }
    return null;
  };

  const fetchPhoton = async () => {
    try {
      const res = await fetch(
        `https://photon.komoot.io/reverse?lon=${lng}&lat=${lat}&limit=15`,
        { signal: controller.signal }
      );
      if (!res.ok) return null;
      const data = await res.json();
      if (data && data.features?.length > 0) {
        const poiCoordsMap: Record<string, { lat: number; lng: number }> = {};

        const scoredCandidates: {
          name: string;
          coords: [number, number]; // [lat, lng]
          dist: number;
          score: number;
        }[] = [];

        for (const f of data.features) {
          const p = f.properties || {};
          let name = (p.name || '').trim();
          if (!name) continue;

          // If bus stop named after a venue, remove bus stop prefix
          if (name.startsWith('Điểm dừng ')) {
            name = name.replace('Điểm dừng ', '').trim();
          }

          const k = p.osm_key;
          const v = p.osm_value;
          const coords = f.geometry?.coordinates; // [lon, lat]
          if (!coords) continue;
          const featLat = coords[1];
          const featLng = coords[0];
          poiCoordsMap[name] = { lat: featLat, lng: featLng };

          const dist = calculateDistanceMeters(lat, lng, featLat, featLng);
          const lowerName = name.toLowerCase();
          const hasVenueKeyword = VENUE_KEYWORDS.some(kw => lowerName.includes(kw));

          // Allow larger search radius for prominent venues (e.g. hospitals, stadiums, convention centers)
          const maxDist = hasVenueKeyword || k === 'amenity' || k === 'leisure' || k === 'tourism' ? 250 : 120;
          if (dist > maxDist) continue;

          let score = 0;
          if (hasVenueKeyword) score += 60;
          if (k === 'amenity' || k === 'healthcare') score += 40;
          else if (k === 'leisure' || k === 'tourism') score += 35;
          else if (k === 'building' && v !== 'yes') score += 25;
          else if (k === 'shop' || k === 'office') score += 15;
          else if (k === 'highway') {
            if (v === 'bus_stop' && hasVenueKeyword) {
              score += 30; // Bus stop carrying venue landmark name
            } else {
              score -= 30; // Plain road
            }
          }

          const isRoadName = lowerName.startsWith('đường ') || lowerName.startsWith('phố ') || lowerName.startsWith('ngõ ') || lowerName.startsWith('ngách ') || lowerName.startsWith('hẻm ');
          if (isRoadName && !hasVenueKeyword) {
            score -= 30;
          }

          // Distance penalty: reduce 1 point every 3 meters
          score -= (dist / 3);

          scoredCandidates.push({
            name,
            coords: [featLat, featLng],
            dist,
            score
          });
        }

        scoredCandidates.sort((a, b) => b.score - a.score);
        const bestCandidate = scoredCandidates.length > 0 && scoredCandidates[0].score > 25 ? scoredCandidates[0] : null;

        const f0 = data.features[0];
        const p0 = f0.properties || {};
        const streetParts = [
          p0.housenumber ? `${p0.housenumber} ${p0.street || ''}` : p0.street,
          p0.district || p0.county,
          p0.city
        ].filter(Boolean);
        const street = streetParts.join(', ') || p0.street || '';

        const allPois = Array.from(new Set(scoredCandidates.map(c => c.name))).slice(0, 5);

        return {
          bestCandidate,
          rawFirstFeatureName: p0.name || null,
          street,
          allPois,
          poiCoordsMap
        };
      }
    } catch { }
    return null;
  };

  try {
    const [nomSettled, photonSettled] = await Promise.allSettled([fetchNominatim(), fetchPhoton()]);
    const nomResult = nomSettled.status === 'fulfilled' ? nomSettled.value : null;
    const photonResult = photonSettled.status === 'fulfilled' ? photonSettled.value : null;

    let snappedLat: number | undefined;
    let snappedLng: number | undefined;
    const poiCoordinates: Record<string, { lat: number; lng: number }> = photonResult?.poiCoordsMap || {};

    // 1. HIGHEST PRIORITY: Snapped POI from Photon entity ranking (e.g. Bệnh viện Phổi Trung ương, Mỹ Đình Stadium,...)
    if (photonResult?.bestCandidate) {
      location = photonResult.bestCandidate.name;
      snappedLat = photonResult.bestCandidate.coords[0];
      snappedLng = photonResult.bestCandidate.coords[1];
    } else if (nomResult?.poiName) {
      // 2. Nominatim venue POI
      location = nomResult.poiName;
      if (nomResult.coords) {
        snappedLat = nomResult.coords[0];
        snappedLng = nomResult.coords[1];
        poiCoordinates[location] = { lat: snappedLat, lng: snappedLng };
      }
    } else if (photonResult?.rawFirstFeatureName && !photonResult.rawFirstFeatureName.startsWith('Đường') && !photonResult.rawFirstFeatureName.startsWith('Phố') && !photonResult.rawFirstFeatureName.startsWith('Ngõ')) {
      location = photonResult.rawFirstFeatureName;
    } else if (nomResult?.rawName && !nomResult.rawName.startsWith('Ngõ') && !nomResult.rawName.startsWith('Ngách')) {
      location = nomResult.rawName;
    } else {
      location = nomResult?.street?.split(',')[0] || photonResult?.street?.split(',')[0] || 'Vị trí đã chọn';
    }

    subAddress = nomResult?.street || photonResult?.street || 'Hà Nội';

    const candidatePois = Array.from(new Set([
      ...(photonResult?.allPois || []),
      ...(nomResult?.poiName ? [nomResult.poiName] : [])
    ])).filter(p => p && p !== location && !p.startsWith('Ngõ') && !p.startsWith('Ngách'));

    pois = candidatePois.slice(0, 4);

    const finalResult: ResolvedPlace = { 
      location: location || 'Vị trí đã chọn', 
      subAddress, 
      nearbyPois: pois,
      poiCoordinates,
      snappedLat,
      snappedLng
    };
    geocodeCache.set(cacheKey, finalResult);
    return finalResult;
  } finally {
    clearTimeout(timeoutId);
  }
}

async function handleMapClick(e: L.LeafletMouseEvent) {
  const { lat, lng } = e.latlng;

  // 1. If user is currently editing or adding a venue in the side panel
  if (venuesStore.isEditingVenueMode && venuesStore.editingVenueData) {
    venuesStore.editingVenueData.latitude = Number(lat.toFixed(6));
    venuesStore.editingVenueData.longitude = Number(lng.toFixed(6));
    
    // Auto reverse geocode details to help the user
    const place = await resolvePlaceDetails(lat, lng);
    if (!venuesStore.editingVenueData.name || venuesStore.editingVenueData.name === 'Địa điểm mới') {
      venuesStore.editingVenueData.name = place.location;
    }
    if (!venuesStore.editingVenueData.subAddress) {
      venuesStore.editingVenueData.subAddress = place.subAddress;
    }
    return;
  }

  // 2. Normal job creation: Pure location pin icon only (NO black/indigo text pill)
  if (draftMarker && map) {
    map.removeLayer(draftMarker);
  }

  const draftIcon = L.divIcon({
    className: 'custom-pick-marker',
    html: `
      <div class="pick-pin-container">
        <div class="pick-pin-teardrop">
          <div class="pick-pin-dot"></div>
        </div>
      </div>
    `,
    iconSize: [0, 0],
    iconAnchor: [0, 0]
  });

  draftMarker = L.marker([lat, lng], { icon: draftIcon }).addTo(map!);

  // Open drawer in Create mode
  jobsStore.openCreate(lat, lng, 'Đang xác định...');

  const place = await resolvePlaceDetails(lat, lng);

  // If a prominent POI was identified, smoothly snap the marker and store coordinates
  if (place.snappedLat != null && place.snappedLng != null) {
    draftMarker.setLatLng([place.snappedLat, place.snappedLng]);
    if (jobsStore.draftLocation) {
      jobsStore.draftLocation.lat = place.snappedLat;
      jobsStore.draftLocation.lng = place.snappedLng;
    }
  }

  if (jobsStore.draftLocation) {
    jobsStore.draftLocation.location = place.location;
    jobsStore.draftLocation.subAddress = place.subAddress;
    jobsStore.draftLocation.nearbyPois = place.nearbyPois;
    jobsStore.draftLocation.poiCoordinates = place.poiCoordinates;
  }
}

// Watch draft location coordinate changes (e.g. when user clicks a nearby POI suggestion chip)
watch(
  [() => jobsStore.draftLocation?.lat, () => jobsStore.draftLocation?.lng],
  ([newLat, newLng]) => {
    if (draftMarker && newLat != null && newLng != null) {
      const cur = draftMarker.getLatLng();
      if (Math.abs(cur.lat - newLat) > 0.00001 || Math.abs(cur.lng - newLng) > 0.00001) {
        draftMarker.setLatLng([newLat, newLng]);
      }
    }
  }
);

// Method to simply zoom to a venue without opening draft job drawer
function zoomToVenue(lat: number, lng: number, zoomLevel = 16) {
  if (!map) return;
  map.setView([lat, lng], zoomLevel, { animate: true });
}

// Method exposed for fly-to from search
function flyTo(lat: number, lng: number, name?: string, subAddress?: string) {
  if (!map) return;
  map.setView([lat, lng], 16, { animate: true });

  if (name) {
    const key = `${lat.toFixed(4)},${lng.toFixed(4)}`;
    geocodeCache.set(key, {
      location: name,
      subAddress: subAddress || '',
      nearbyPois: [name],
      snappedLat: lat,
      snappedLng: lng
    });
  }
  
  handleMapClick({
    latlng: L.latLng(lat, lng),
    layerPoint: L.point(0, 0),
    containerPoint: L.point(0, 0),
    originalEvent: new MouseEvent('click')
  } as any);

  if (name && jobsStore.draftLocation) {
    jobsStore.draftLocation.location = name;
    if (subAddress) jobsStore.draftLocation.subAddress = subAddress;
  }
}

defineExpose({
  flyTo,
  zoomToVenue,
  setMapType
});
</script>

<template>
  <div class="w-full h-full relative">
    <div ref="mapContainer" class="w-full h-full z-10"></div>

    <!-- GOOGLE MAPS BOTTOM-RIGHT LAYER SWITCHER -->
    <div class="absolute bottom-6 right-5 z-20 pointer-events-auto select-none">
      <button 
        type="button"
        @click="setMapType(mapType === 'roadmap' ? 'satellite' : 'roadmap')"
        class="group relative w-14 h-14 rounded-2xl overflow-hidden border-2 border-white shadow-lg ring-1 ring-slate-200/80 hover:ring-2 hover:ring-blue-500 hover:scale-105 active:scale-95 transition cursor-pointer"
        :title="mapType === 'roadmap' ? 'Chuyển sang bản đồ Vệ tinh' : 'Chuyển sang bản đồ Google thường'"
      >
        <!-- Thumbnail preview image -->
        <img 
          :src="mapType === 'roadmap' 
            ? 'https://mt1.google.com/vt/lyrs=y&x=13410&y=7410&z=14' 
            : 'https://mt1.google.com/vt/lyrs=m&x=13410&y=7410&z=14'"
          :alt="mapType === 'roadmap' ? 'Vệ tinh' : 'Bản đồ'"
          class="w-full h-full object-cover transition group-hover:scale-110"
        />
        <!-- Label banner overlay -->
        <div class="absolute bottom-0 inset-x-0 bg-slate-900/80 backdrop-blur-xs py-0.5 text-center text-[10px] font-extrabold text-white">
          {{ mapType === 'roadmap' ? 'Vệ tinh' : 'Bản đồ' }}
        </div>
      </button>
    </div>
  </div>
</template>
