<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from './stores/auth';
import { useJobsStore } from './stores/jobs';
import MapView from './components/MapView.vue';
import FloatingHeader from './components/FloatingHeader.vue';
import JobSideDrawer from './components/JobSideDrawer.vue';
import ProfileModal from './components/ProfileModal.vue';
import PinLoginModal from './components/PinLoginModal.vue';

const authStore = useAuthStore();
const jobsStore = useJobsStore();

const mapViewRef = ref<InstanceType<typeof MapView> | null>(null);
const showProfileModal = ref(false);
const showLoginModal = ref(false);

onMounted(async () => {
  await authStore.fetchTeamMembers();

  if (authStore.isAuthenticated) {
    await authStore.fetchCurrentUser();
    await jobsStore.loadJobs();
  } else {
    showLoginModal.value = true;
  }

  window.addEventListener('auth:unauthorized', () => {
    showLoginModal.value = true;
  });
});

function onFlyTo(place: { lat: number; lng: number; mainName: string; subAddress: string }) {
  mapViewRef.value?.flyTo(place.lat, place.lng, place.mainName, place.subAddress);
}

function handleLoginSuccess() {
  showLoginModal.value = false;
  jobsStore.loadJobs();
}
</script>

<template>
  <main class="relative w-screen h-screen overflow-hidden bg-slate-900 select-none">
    
    <!-- 1. FULL-SCREEN 100VW x 100VH MAP -->
    <MapView 
      ref="mapViewRef" 
    />

    <!-- 2. FLOATING TOP CONTROLS (SEARCH, FILTERS, EXCEL, PROFILE) -->
    <FloatingHeader 
      v-if="authStore.isAuthenticated"
      @open-profile="showProfileModal = true"
      @open-login="showLoginModal = true"
      @fly-to="onFlyTo"
    />

    <!-- 3. ADAPTIVE SIDE DRAWER (CREATE & EDIT JOB) -->
    <transition name="drawer">
      <JobSideDrawer 
        v-if="jobsStore.drawerOpen" 
      />
    </transition>

    <!-- 4. PROFILE & FIXED SCHEDULE MODAL -->
    <transition name="fade">
      <ProfileModal 
        v-if="showProfileModal" 
        @close="showProfileModal = false" 
      />
    </transition>

    <!-- 5. LIGHTWEIGHT PIN/JWT LOGIN MODAL -->
    <transition name="fade">
      <PinLoginModal 
        v-if="showLoginModal" 
        :closable="authStore.isAuthenticated"
        @close="handleLoginSuccess" 
      />
    </transition>

    <!-- Loading Indicator -->
    <div 
      v-if="jobsStore.loading" 
      class="absolute bottom-4 right-4 z-20 bg-white/90 backdrop-blur-md border border-slate-200/80 px-3 py-1.5 rounded-full shadow-lg text-[11px] font-bold text-slate-700 flex items-center gap-2"
    >
      <span class="w-2 h-2 rounded-full bg-indigo-600 animate-ping"></span>
      Đang đồng bộ dữ liệu...
    </div>

  </main>
</template>
