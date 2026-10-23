import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { SavedVenueDto, SaveVenueRequest } from '../types';
import { venuesApi } from '../services/api';

function calculateDistanceMeters(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371000;
  const dLat = (lat2 - lat1) * Math.PI / 180;
  const dLon = (lon2 - lon1) * Math.PI / 180;
  const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) + 
            Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) * 
            Math.sin(dLon / 2) * Math.sin(dLon / 2);
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

export const useVenuesStore = defineStore('venues', () => {
  const venues = ref<SavedVenueDto[]>([]);
  const loading = ref(false);

  async function loadVenues() {
    loading.value = true;
    try {
      venues.value = await venuesApi.getVenues();
    } catch (err) {
      console.error('Failed to load saved venues', err);
    } finally {
      loading.value = false;
    }
  }

  async function saveVenue(payload: SaveVenueRequest) {
    try {
      const saved = await venuesApi.createVenue(payload);
      const idx = venues.value.findIndex(v => v.id === saved.id);
      if (idx >= 0) {
        venues.value[idx] = saved;
      } else {
        venues.value.unshift(saved);
      }
      return saved;
    } catch (err) {
      console.error('Failed to save venue', err);
      throw err;
    }
  }

  async function updateVenue(id: number, payload: SaveVenueRequest) {
    try {
      const updated = await venuesApi.updateVenue(id, payload);
      const idx = venues.value.findIndex(v => v.id === id);
      if (idx >= 0) {
        venues.value[idx] = updated;
      }
      return updated;
    } catch (err) {
      console.error('Failed to update venue', err);
      throw err;
    }
  }

  async function deleteVenue(id: number) {
    try {
      await venuesApi.deleteVenue(id);
      venues.value = venues.value.filter(v => v.id !== id);
    } catch (err) {
      console.error('Failed to delete venue', err);
      throw err;
    }
  }

  // Find a saved venue near coordinates within maxDistMeters (default 180m)
  function findNearbyVenue(lat: number, lng: number, maxDistMeters = 180): SavedVenueDto | null {
    let bestVenue: SavedVenueDto | null = null;
    let minDist = maxDistMeters;

    for (const v of venues.value) {
      const dist = calculateDistanceMeters(lat, lng, v.latitude, v.longitude);
      if (dist < minDist) {
        minDist = dist;
        bestVenue = v;
      }
    }

    return bestVenue;
  }

  // Active venue creation/editing state (when editing in side panel)
  const isEditingVenueMode = ref(false);
  const editingVenueData = ref<{
    id: number | null;
    name: string;
    subAddress: string;
    latitude: number;
    longitude: number;
  } | null>(null);

  function startEditingVenue(venue?: SavedVenueDto) {
    isEditingVenueMode.value = true;
    if (venue) {
      editingVenueData.value = {
        id: venue.id,
        name: venue.name,
        subAddress: venue.subAddress || '',
        latitude: Number(venue.latitude),
        longitude: Number(venue.longitude)
      };
    } else {
      editingVenueData.value = {
        id: null,
        name: '',
        subAddress: '',
        latitude: 21.015,
        longitude: 105.815
      };
    }
  }

  function stopEditingVenue() {
    isEditingVenueMode.value = false;
    editingVenueData.value = null;
  }

  return {
    venues,
    loading,
    isEditingVenueMode,
    editingVenueData,
    startEditingVenue,
    stopEditingVenue,
    loadVenues,
    saveVenue,
    updateVenue,
    deleteVenue,
    findNearbyVenue
  };
});
