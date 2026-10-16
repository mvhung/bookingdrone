<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';
import L from 'leaflet';
import { useJobsStore } from '../stores/jobs';
import { useAuthStore } from '../stores/auth';
import type { JobResponseDto } from '../types';
import { SERVICE_CATALOG } from '../types';

const emit = defineEmits<{
  (e: 'marker-click', job: JobResponseDto): void;
}>();

const jobsStore = useJobsStore();
const authStore = useAuthStore();

const mapContainer = ref<HTMLElement | null>(null);
let map: L.Map | null = null;
let markers: L.Marker[] = [];
let draftMarker: L.Marker | null = null;

onMounted(() => {
  initMap();
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

  L.control.zoom({ position: 'bottomleft' }).addTo(map);

  // Esri World Street Map (Crisp, clean tiles perfect for navigation)
  L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}', {
    maxZoom: 19
  }).addTo(map);

  map.on('click', handleMapClick);

  renderPins();
}

// Watch visible jobs from store to re-render pins
watch(() => jobsStore.visibleJobs, () => {
  renderPins();
}, { deep: true });

// Watch drawer closing to remove draft marker if any
watch(() => jobsStore.drawerOpen, (isOpen) => {
  if (!isOpen && draftMarker && map) {
    map.removeLayer(draftMarker);
    draftMarker = null;
  }
});

function getServiceIcon(code: string) {
  return SERVICE_CATALOG.find(s => s.code === code)?.icon || '📍';
}

function renderPins() {
  if (!map) return;

  // Clear existing markers
  markers.forEach(m => map!.removeLayer(m));
  markers = [];

  const currentUserId = authStore.currentUser?.id;

  jobsStore.visibleJobs.forEach(job => {
    const isOwner = job.ownerUserId === currentUserId;
    const svIcon = getServiceIcon(job.serviceCode);
    const crewNames = job.assignments.map(a => a.userName).join(', ');

    const html = `
      <div class="custom-pin ${isOwner ? 'custom-pin-owner' : ''}">
        <span class="text-xs">${svIcon}</span>
        <span class="font-bold">${job.startTime} • ${job.locationName}</span>
        <span style="opacity: 0.8; font-weight: 400; font-size: 10px;">(${crewNames || 'Chưa gán'})</span>
      </div>
    `;

    const icon = L.divIcon({
      className: '',
      html,
      iconAnchor: [20, 20]
    });

    const marker = L.marker([job.latitude, job.longitude], { icon }).addTo(map!);
    marker.on('click', (e) => {
      L.DomEvent.stopPropagation(e);
      jobsStore.openEdit(job);
      emit('marker-click', job);
    });

    markers.push(marker);
  });
}

// Reverse Geocoding with Overpass (85m venue POIs) + Photon fallback
async function resolvePlaceDetails(lat: number, lng: number) {
  let location = 'Điểm đã chọn trên bản đồ';
  let subAddress = 'Hà Nội';
  let pois: string[] = [];

  try {
    const q = `[out:json][timeout:5];(node(around:85,${lat},${lng})["name"];way(around:85,${lat},${lng})["name"];);out tags center 6;`;
    const [overpass, photon] = await Promise.all([
      fetch(`https://overpass-api.de/api/interpreter?data=${encodeURIComponent(q)}`).then(r => r.json()).catch(() => null),
      fetch(`https://photon.komoot.io/reverse?lon=${lng}&lat=${lat}`).then(r => r.json()).catch(() => null)
    ]);

    if (overpass && overpass.elements) {
      pois = Array.from(new Set(
        overpass.elements
          .filter((e: any) => e.tags?.name && !e.tags?.highway)
          .map((e: any) => e.tags.name as string)
      ));
    }

    let street = '';
    let photonName = '';
    if (photon && photon.features?.length > 0) {
      const p = photon.features[0].properties;
      photonName = p.name || '';
      street = [p.street, p.district || p.city].filter(Boolean).join(', ');
    }

    if (pois.length > 0) {
      location = pois[0];
      subAddress = street || 'Hà Nội';
    } else if (photonName) {
      location = photonName;
      subAddress = street;
    } else if (street) {
      location = street;
    }
  } catch (err) {
    console.error('Reverse geocode error', err);
  }

  return { location, subAddress, nearbyPois: pois.slice(0, 4) };
}

async function handleMapClick(e: L.LeafletMouseEvent) {
  const { lat, lng } = e.latlng;

  // Show a pulsating draft marker
  if (draftMarker && map) {
    map.removeLayer(draftMarker);
  }

  const draftIcon = L.divIcon({
    className: '',
    html: `
      <div class="custom-pin bg-indigo-600 border-2 border-white animate-bounce">
        <span>📍</span>
        <span>Đang chọn vị trí...</span>
      </div>
    `,
    iconAnchor: [20, 20]
  });

  draftMarker = L.marker([lat, lng], { icon: draftIcon }).addTo(map!);

  // Open drawer in Create mode
  jobsStore.openCreate(lat, lng, 'Đang xác định tên địa điểm...');

  const place = await resolvePlaceDetails(lat, lng);
  if (jobsStore.draftLocation) {
    jobsStore.draftLocation.location = place.location;
    jobsStore.draftLocation.subAddress = place.subAddress;
    jobsStore.draftLocation.nearbyPois = place.nearbyPois;
  }
}

// Method exposed for fly-to from search
function flyTo(lat: number, lng: number, name?: string, subAddress?: string) {
  if (!map) return;
  map.setView([lat, lng], 16, { animate: true });
  
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
  flyTo
});
</script>

<template>
  <div class="w-full h-full relative">
    <div ref="mapContainer" class="w-full h-full z-10"></div>
  </div>
</template>
