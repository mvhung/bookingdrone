import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import type { JobResponseDto, SaveJobRequest } from '../types';
import { jobsApi } from '../services/api';
import { useAuthStore } from './auth';

export const useJobsStore = defineStore('jobs', () => {
  const authStore = useAuthStore();
  const jobs = ref<JobResponseDto[]>([]);
  const loading = ref(false);

  const selectedMonth = ref('2026-10');
  const selectedDate = ref('2026-10-18');
  const viewAllMonth = ref(true);

  // Side Drawer state
  const drawerOpen = ref(false);
  const drawerMode = ref<'create' | 'edit'>('create');
  const activeJob = ref<JobResponseDto | null>(null);

  // Draft pin for new job
  const draftLocation = ref<{
    lat: number;
    lng: number;
    location: string;
    subAddress: string;
    nearbyPois: string[];
  } | null>(null);

  // Filtered jobs visible on map
  const visibleJobs = computed(() => {
    return jobs.value.filter(j => {
      if (viewAllMonth.value) {
        return j.eventDate.startsWith(selectedMonth.value);
      }
      return j.eventDate === selectedDate.value;
    });
  });

  async function loadJobs() {
    if (!authStore.isAuthenticated) return;
    loading.value = true;
    try {
      const data = await jobsApi.getJobs(
        viewAllMonth.value ? selectedMonth.value : undefined,
        !viewAllMonth.value ? selectedDate.value : undefined
      );
      jobs.value = data;
    } catch (err: any) {
      console.error('Failed to load jobs', err);
    } finally {
      loading.value = false;
    }
  }

  async function createJob(payload: SaveJobRequest) {
    loading.value = true;
    try {
      const created = await jobsApi.createJob(payload);
      jobs.value.push(created);
      closeDrawer();
      return created;
    } catch (err: any) {
      console.error('Failed to create job', err);
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function updateJob(id: number, payload: SaveJobRequest) {
    loading.value = true;
    try {
      const updated = await jobsApi.updateJob(id, payload);
      const idx = jobs.value.findIndex(j => j.id === id);
      if (idx >= 0) {
        jobs.value[idx] = updated;
      }
      closeDrawer();
      return updated;
    } catch (err: any) {
      console.error('Failed to update job', err);
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function deleteJob(id: number) {
    loading.value = true;
    try {
      await jobsApi.deleteJob(id);
      jobs.value = jobs.value.filter(j => j.id !== id);
      closeDrawer();
    } catch (err: any) {
      console.error('Failed to delete job', err);
      throw err;
    } finally {
      loading.value = false;
    }
  }

  function openCreate(lat: number, lng: number, location = 'Đang lấy địa điểm...', subAddress = '', nearbyPois: string[] = []) {
    drawerMode.value = 'create';
    activeJob.value = null;
    draftLocation.value = {
      lat,
      lng,
      location,
      subAddress,
      nearbyPois
    };
    drawerOpen.value = true;
  }

  function openEdit(job: JobResponseDto) {
    drawerMode.value = 'edit';
    activeJob.value = job;
    draftLocation.value = null;
    drawerOpen.value = true;
  }

  function closeDrawer() {
    drawerOpen.value = false;
    activeJob.value = null;
    draftLocation.value = null;
  }

  return {
    jobs,
    loading,
    selectedMonth,
    selectedDate,
    viewAllMonth,
    drawerOpen,
    drawerMode,
    activeJob,
    draftLocation,
    visibleJobs,
    loadJobs,
    createJob,
    updateJob,
    deleteJob,
    openCreate,
    openEdit,
    closeDrawer
  };
});
