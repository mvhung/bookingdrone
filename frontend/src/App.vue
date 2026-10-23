<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAuthStore } from './stores/auth';
import { useJobsStore } from './stores/jobs';
import MapView from './components/MapView.vue';
import GoogleNavRail from './components/GoogleNavRail.vue';
import FloatingHeader from './components/FloatingHeader.vue';
import CalendarSidePanel from './components/CalendarSidePanel.vue';
import CrewAdminSidePanel from './components/CrewAdminSidePanel.vue';
import JobSideDrawer from './components/JobSideDrawer.vue';
import ProfileModal from './components/ProfileModal.vue';
import PinLoginModal from './components/PinLoginModal.vue';

const authStore = useAuthStore();
const jobsStore = useJobsStore();

const mapViewRef = ref<InstanceType<typeof MapView> | null>(null);
const floatingHeaderRef = ref<InstanceType<typeof FloatingHeader> | null>(null);

const showProfileModal = ref(false);
const showLoginModal = ref(false);

// Active Left Side Panel: 'none' | 'calendar' | 'admin'
const activeLeftPanel = ref<'none' | 'calendar' | 'admin'>('none');

const activeNavTab = computed<'map' | 'calendar' | 'admin' | 'profile'>(() => {
  if (activeLeftPanel.value === 'calendar') return 'calendar';
  if (activeLeftPanel.value === 'admin') return 'admin';
  if (showProfileModal.value) return 'profile';
  return 'map';
});

onMounted(async () => {
  await authStore.fetchServices();

  if (authStore.isAuthenticated) {
    await authStore.fetchCurrentUser();
    await Promise.all([
      authStore.fetchTeamMembers(),
      jobsStore.loadJobs()
    ]);
  } else {
    showLoginModal.value = true;
  }

  window.addEventListener('auth:unauthorized', () => {
    showLoginModal.value = true;
  });
});

function handleNavSelect(tab: 'map' | 'calendar' | 'admin' | 'profile') {
  if (tab === 'map') {
    activeLeftPanel.value = 'none';
  } else if (tab === 'calendar') {
    activeLeftPanel.value = activeLeftPanel.value === 'calendar' ? 'none' : 'calendar';
  } else if (tab === 'admin') {
    activeLeftPanel.value = activeLeftPanel.value === 'admin' ? 'none' : 'admin';
  } else if (tab === 'profile') {
    showProfileModal.value = true;
  }
}

function onFlyTo(place: { lat: number; lng: number; mainName?: string; subAddress?: string; zoomOnly?: boolean }) {
  if (place.zoomOnly) {
    mapViewRef.value?.zoomToVenue(place.lat, place.lng);
  } else {
    mapViewRef.value?.flyTo(place.lat, place.lng, place.mainName, place.subAddress);
  }
}

function onSelectJob(job: any) {
  jobsStore.openEdit(job);
}

async function handleLoginSuccess() {
  showLoginModal.value = false;
  await Promise.all([
    authStore.fetchServices(),
    authStore.fetchTeamMembers(),
    jobsStore.loadJobs()
  ]);
}

function handleExportExcel() {
  floatingHeaderRef.value?.exportMonthlyExcel();
}
</script>

<template>
  <main class="relative w-screen h-screen overflow-hidden bg-slate-900 select-none">
    
    <!-- 1. GOOGLE MAPS LEFT NAVIGATION RAIL -->
    <GoogleNavRail 
      v-if="authStore.isAuthenticated"
      :active-tab="activeNavTab"
      @select-tab="handleNavSelect"
      @export-excel="handleExportExcel"
      @open-login="showLoginModal = true"
    />

    <!-- 2. FULL-SCREEN 100VW x 100VH MAP (Always present and interactive) -->
    <MapView 
      ref="mapViewRef" 
    />

    <!-- 3. GOOGLE MAPS CALENDAR / SCHEDULE LEFT SIDE PANEL -->
    <transition name="drawer-left">
      <CalendarSidePanel 
        v-if="activeLeftPanel === 'calendar'"
        @close="activeLeftPanel = 'none'"
        @fly-to="onFlyTo"
        @select-job="onSelectJob"
      />
    </transition>

    <!-- 4. GOOGLE MAPS CREW & SERVICES (ADMIN) LEFT SIDE PANEL -->
    <transition name="drawer-left">
      <CrewAdminSidePanel 
        v-if="activeLeftPanel === 'admin'"
        @close="activeLeftPanel = 'none'"
        @fly-to="onFlyTo"
      />
    </transition>

    <!-- 5. FLOATING TOP SEARCH & FILTER BAR + TOP-RIGHT GOOGLE USER PROFILE -->
    <FloatingHeader 
      v-if="authStore.isAuthenticated"
      ref="floatingHeaderRef"
      :has-left-panel="activeLeftPanel !== 'none'"
      @open-calendar="activeLeftPanel = 'calendar'"
      @open-admin="activeLeftPanel = 'admin'"
      @open-profile="showProfileModal = true"
      @open-login="showLoginModal = true"
      @fly-to="onFlyTo"
    />

    <!-- 6. ADAPTIVE RIGHT SIDE DRAWER (CREATE & EDIT JOB) -->
    <transition name="drawer">
      <JobSideDrawer 
        v-if="jobsStore.drawerOpen" 
      />
    </transition>

    <!-- 7. PROFILE & FIXED SCHEDULE MODAL -->
    <transition name="fade">
      <ProfileModal 
        v-if="showProfileModal" 
        @open-calendar="activeLeftPanel = 'calendar'"
        @close="showProfileModal = false" 
      />
    </transition>

    <!-- 8. LIGHTWEIGHT PIN/JWT LOGIN MODAL -->
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
      :class="activeLeftPanel !== 'none' ? 'left-[430px] sm:left-[490px]' : 'left-20 sm:left-24'"
      class="absolute bottom-5 z-20 bg-white/95 backdrop-blur-md border border-slate-200/90 px-3.5 py-1.5 rounded-full shadow-lg text-[11px] font-bold text-slate-700 flex items-center gap-2 transition-all duration-300"
    >
      <span class="w-2 h-2 rounded-full bg-blue-600 animate-ping"></span>
      Đang đồng bộ dữ liệu...
    </div>

  </main>
</template>
