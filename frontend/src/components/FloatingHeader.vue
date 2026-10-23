<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import * as XLSX from 'xlsx';
import { useAuthStore } from '../stores/auth';
import { useJobsStore } from '../stores/jobs';
import { SERVICE_CATALOG } from '../types';

const props = withDefaults(
  defineProps<{
    viewMode?: 'map' | 'calendar';
    hasLeftPanel?: boolean;
  }>(),
  {
    viewMode: 'map',
    hasLeftPanel: false
  }
);

const emit = defineEmits<{
  (e: 'open-profile'): void;
  (e: 'open-login'): void;
  (e: 'open-calendar'): void;
  (e: 'open-admin'): void;
  (e: 'change-view-mode', mode: 'map' | 'calendar'): void;
  (e: 'fly-to', coords: { lat: number; lng: number; mainName: string; subAddress: string }): void;
}>();

const authStore = useAuthStore();
const jobsStore = useJobsStore();

// Category Carousel Horizontal Scroll Management
const categoryScrollRef = ref<HTMLElement | null>(null);
const canScrollLeft = ref(false);
const canScrollRight = ref(false);

function updateScrollButtons() {
  if (!categoryScrollRef.value) return;
  const { scrollLeft, scrollWidth, clientWidth } = categoryScrollRef.value;
  canScrollLeft.value = Math.ceil(scrollLeft) > 8;
  canScrollRight.value = Math.ceil(scrollLeft + clientWidth) < Math.floor(scrollWidth) - 8;
}

function scrollCategories(direction: 'left' | 'right') {
  if (!categoryScrollRef.value) return;
  const step = 180;
  categoryScrollRef.value.scrollBy({
    left: direction === 'left' ? -step : step,
    behavior: 'smooth'
  });
  setTimeout(updateScrollButtons, 220);
}

import { useVenuesStore } from '../stores/venues';
import type { SavedVenueDto } from '../types';

const venuesStore = useVenuesStore();

// Saved Venues Dropdown State
const showVenuesDropdown = ref(false);
const venueFilterQuery = ref('');
const venuesDropdownRef = ref<HTMLElement | null>(null);

const filteredVenues = computed(() => {
  const q = venueFilterQuery.value.trim().toLowerCase();
  if (!q) return venuesStore.venues;
  return venuesStore.venues.filter(v => 
    v.name.toLowerCase().includes(q) || 
    (v.subAddress && v.subAddress.toLowerCase().includes(q))
  );
});

function pickVenue(v: SavedVenueDto) {
  showVenuesDropdown.value = false;
  venueFilterQuery.value = '';
  emit('fly-to', {
    lat: v.latitude,
    lng: v.longitude,
    mainName: v.name,
    subAddress: v.subAddress || ''
  });
}

async function removeVenue(id: number, e: MouseEvent) {
  e.stopPropagation();
  if (confirm('Bạn có chắc muốn xóa địa điểm này khỏi danh sách đã lưu?')) {
    await venuesStore.deleteVenue(id);
  }
}

function handleOutsideClick(e: MouseEvent) {
  if (venuesDropdownRef.value && !venuesDropdownRef.value.contains(e.target as Node)) {
    showVenuesDropdown.value = false;
  }
}

onMounted(() => {
  window.addEventListener('resize', updateScrollButtons);
  window.addEventListener('click', handleOutsideClick);
  setTimeout(updateScrollButtons, 200);
});

onUnmounted(() => {
  window.removeEventListener('resize', updateScrollButtons);
  window.removeEventListener('click', handleOutsideClick);
});

// Service Filter list
const serviceList = computed(() => {
  return authStore.services.length > 0 ? authStore.services : SERVICE_CATALOG;
});

function selectService(code: string) {
  if (jobsStore.selectedService === code) {
    jobsStore.selectedService = 'ALL';
  } else {
    jobsStore.selectedService = code;
  }
}

// Watch filters to reload data from API if needed
watch(() => jobsStore.selectedMonth, () => {
  jobsStore.loadJobs();
});

watch(() => jobsStore.selectedDate, () => {
  if (!jobsStore.viewAllMonth) {
    jobsStore.loadJobs();
  }
});

watch(() => jobsStore.viewAllMonth, () => {
  jobsStore.loadJobs();
});

function getServiceLabel(code: string) {
  const s = serviceList.value.find(x => x.code === code);
  return s ? `${s.icon} ${s.name}` : code;
}

function formatDateVN(dateStr: string) {
  if (!dateStr) return '';
  return dateStr.split('-').reverse().join('/');
}

// Date Picker & Month Navigation Helpers
const datePickerInputRef = ref<HTMLInputElement | null>(null);

const monthDisplay = computed(() => {
  const parts = jobsStore.selectedMonth.split('-');
  return parts.length === 2 ? parts[1] : '';
});

const isTodaySelected = computed(() => {
  const now = new Date();
  const yyyy = now.getFullYear();
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  const todayStr = `${yyyy}-${mm}-${dd}`;
  return jobsStore.selectedDate === todayStr;
});

function toggleAllMonth(val: boolean) {
  jobsStore.viewAllMonth = val;
  jobsStore.loadJobs();
}

function triggerDatePicker() {
  if (datePickerInputRef.value) {
    try {
      if (typeof datePickerInputRef.value.showPicker === 'function') {
        datePickerInputRef.value.showPicker();
      } else {
        datePickerInputRef.value.focus();
        datePickerInputRef.value.click();
      }
    } catch {
      datePickerInputRef.value.click();
    }
  }
}

function onDatePicked(e: Event) {
  const val = (e.target as HTMLInputElement).value;
  if (val) {
    jobsStore.selectedDate = val;
    jobsStore.selectedMonth = val.substring(0, 7);
    jobsStore.viewAllMonth = false;
    jobsStore.loadJobs();
  }
}

function setToday() {
  const now = new Date();
  const yyyy = now.getFullYear();
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  jobsStore.selectedDate = `${yyyy}-${mm}-${dd}`;
  jobsStore.selectedMonth = `${yyyy}-${mm}`;
  jobsStore.viewAllMonth = false;
  jobsStore.loadJobs();
}

function prevMonth() {
  const parts = jobsStore.selectedMonth.split('-');
  if (parts.length === 2) {
    let y = parseInt(parts[0]);
    let m = parseInt(parts[1]) - 1;
    if (m < 1) {
      m = 12;
      y -= 1;
    }
    jobsStore.selectedMonth = `${y}-${String(m).padStart(2, '0')}`;
    jobsStore.loadJobs();
  }
}

function nextMonth() {
  const parts = jobsStore.selectedMonth.split('-');
  if (parts.length === 2) {
    let y = parseInt(parts[0]);
    let m = parseInt(parts[1]) + 1;
    if (m > 12) {
      m = 1;
      y += 1;
    }
    jobsStore.selectedMonth = `${y}-${String(m).padStart(2, '0')}`;
    jobsStore.loadJobs();
  }
}

// Export Monthly Excel
function exportMonthlyExcel() {
  const currentUserId = authStore.currentUser?.id;
  if (!currentUserId) return;

  const monthJobs = jobsStore.jobs.filter(j => 
    j.eventDate.startsWith(jobsStore.selectedMonth)
  );

  if (monthJobs.length === 0) {
    alert(`Không có công việc nào trong tháng ${jobsStore.selectedMonth} để xuất!`);
    return;
  }

  const rows = monthJobs.map((j, i) => {
    const isOwner = j.ownerUserId === currentUserId;
    const myAssignment = j.assignments.find(a => a.userId === currentUserId);
    const totalCrewCost = j.assignments.reduce((sum, a) => sum + (a.castAmount || 0), 0);
    const crewNames = j.assignments.map(a => a.userName)
      .concat(j.externalWorkersNote ? [j.externalWorkersNote] : [])
      .join(', ');

    return {
      'STT': i + 1,
      'Ngày': formatDateVN(j.eventDate),
      'Khung giờ': `${j.startTime} - ${j.endTime}`,
      'Địa điểm (Job Title)': j.locationName,
      'Địa chỉ phụ': j.subAddress,
      'Dịch vụ': getServiceLabel(j.serviceCode),
      'Chủ Job': j.ownerUserName || 'Khách lẻ',
      'Đội ngũ chạy': crewNames,
      'Cát-xê của tôi (VNĐ)': myAssignment?.castAmount ?? (isOwner ? 'Chủ Job' : 0),
      'Trạng thái TT': myAssignment ? (myAssignment.isPaid ? 'Đã TT' : 'Chờ TT') : (isOwner ? 'Chủ Job' : ''),
      'Tổng thu khách (VNĐ)': isOwner ? (j.clientTotalRevenue ?? 'Chưa nhập') : 'Bảo mật',
      'Lãi chênh (VNĐ)': (isOwner && j.clientTotalRevenue != null) ? (j.clientTotalRevenue - totalCrewCost) : '',
      'Ghi chú': j.note || ''
    };
  });

  const worksheet = XLSX.utils.json_to_sheet(rows);

  worksheet['!cols'] = [
    { wch: 6 },  // STT
    { wch: 12 }, // Ngày
    { wch: 15 }, // Khung giờ
    { wch: 30 }, // Địa điểm
    { wch: 25 }, // Địa chỉ phụ
    { wch: 14 }, // Dịch vụ
    { wch: 15 }, // Chủ Job
    { wch: 25 }, // Đội ngũ chạy
    { wch: 18 }, // Cát-xê của tôi
    { wch: 14 }, // Trạng thái TT
    { wch: 20 }, // Tổng thu khách
    { wch: 16 }, // Lãi chênh
    { wch: 30 }  // Ghi chú
  ];

  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, worksheet, `Thang_${jobsStore.selectedMonth}`);
  
  const userName = authStore.currentUser?.name || 'User';
  XLSX.writeFile(workbook, `Lich_Chay_${userName}_${jobsStore.selectedMonth}.xlsx`);
}

defineExpose({
  exportMonthlyExcel
});
</script>

<template>
  <header 
    :class="hasLeftPanel ? 'left-[420px] sm:left-[490px] w-[calc(100vw-436px)] sm:w-[calc(100vw-506px)]' : 'left-20 sm:left-24 w-[calc(100vw-96px)] sm:w-[calc(100vw-112px)]'"
    class="absolute top-3 z-20 flex items-center justify-between gap-2 pointer-events-none select-none transition-all duration-300"
  >
    
    <!-- LEFT CLUSTER: SAVED VENUES BUTTON + SCROLLABLE CATEGORY CAROUSEL (WITH GG MAPS ARROWS) -->
    <div class="flex items-center gap-2 min-w-0 flex-1 pointer-events-auto">
      
      <!-- 1. Saved Venues Dropdown Button -->
      <div class="relative shrink-0" ref="venuesDropdownRef">
        <button 
          @click="showVenuesDropdown = !showVenuesDropdown" 
          type="button"
          class="bg-white/98 backdrop-blur-md border border-slate-200/90 shadow-sm hover:shadow-md rounded-full px-3.5 py-1.5 flex items-center gap-2 text-xs font-bold text-slate-800 hover:text-indigo-600 transition cursor-pointer select-none active:scale-95"
          title="Xem và chọn nhanh các địa điểm tiệc đã lưu"
        >
          <span class="text-indigo-600 text-sm shrink-0">🏛️</span>
          <span class="hidden sm:inline">Địa điểm tiệc</span>
          <span class="sm:hidden">Địa điểm</span>
          <span class="px-1.5 py-0.2 rounded-full bg-indigo-50 text-indigo-700 font-extrabold text-[10px]">
            {{ venuesStore.venues.length }}
          </span>
          <span class="text-[9px] text-slate-400">▼</span>
        </button>

        <!-- Venues Dropdown Menu -->
        <div 
          v-if="showVenuesDropdown"
          class="absolute left-0 mt-2 w-72 sm:w-84 bg-white border border-slate-200 rounded-3xl shadow-2xl overflow-hidden z-50 animate-in fade-in slide-in-from-top-1 divide-y divide-slate-100"
        >
          <!-- Header with local search -->
          <div class="p-3 bg-slate-50/80">
            <div class="flex items-center justify-between mb-2">
              <span class="font-bold text-xs text-slate-800 flex items-center gap-1.5">
                <span>🏛️</span> Địa điểm tiệc đã lưu
              </span>
              <span class="text-[10px] text-slate-500 font-semibold">{{ filteredVenues.length }} địa điểm</span>
            </div>
            <div class="relative">
              <input 
                v-model="venueFilterQuery"
                placeholder="Lọc trong các địa điểm đã lưu..."
                class="w-full bg-white border border-slate-200 rounded-xl px-2.5 py-1 text-xs text-slate-800 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500"
              />
              <span v-if="venueFilterQuery" @click="venueFilterQuery = ''" class="absolute right-2 top-1.5 text-xs text-slate-400 cursor-pointer">✕</span>
            </div>
          </div>

          <!-- Venues List -->
          <div class="max-h-64 overflow-y-auto divide-y divide-slate-100">
            <div 
              v-for="v in filteredVenues" 
              :key="v.id"
              @click="pickVenue(v)"
              class="p-2.5 hover:bg-indigo-50/70 cursor-pointer transition text-xs flex items-start justify-between gap-2 group"
            >
              <div class="min-w-0 flex-1">
                <div class="font-bold text-slate-900 group-hover:text-indigo-600 transition truncate">
                  {{ v.name }}
                </div>
                <div class="text-[10px] text-slate-500 truncate">
                  {{ v.subAddress || 'Chưa có địa chỉ chi tiết' }}
                </div>
              </div>
              <div class="flex items-center gap-1.5 shrink-0 pt-0.5">
                <span class="text-[9px] px-1.5 py-0.5 rounded-full bg-slate-100 text-slate-600 font-bold">
                  {{ v.usageCount }} job
                </span>
                <button 
                  @click="removeVenue(v.id, $event)"
                  type="button"
                  class="text-slate-300 hover:text-rose-500 text-xs px-1 rounded transition opacity-0 group-hover:opacity-100"
                  title="Xóa địa điểm này"
                >
                  ✕
                </button>
              </div>
            </div>

            <div v-if="filteredVenues.length === 0" class="p-4 text-center text-xs text-slate-400">
              Không tìm thấy địa điểm phù hợp
            </div>
          </div>
        </div>
      </div>

      <!-- 2. Scrollable Category Chips Carousel with Left/Right Arrows (Google Maps style) -->
      <div class="flex items-center gap-1 min-w-0 flex-1">
        <!-- Left scroll arrow button (Outside, static flex item, NEVER overlaps pills!) -->
        <button 
          v-if="canScrollLeft"
          type="button"
          @click="scrollCategories('left')"
          class="w-7 h-7 rounded-full bg-white border border-slate-200 shadow-sm hover:bg-slate-100 active:scale-95 flex items-center justify-center text-slate-700 font-bold text-xs shrink-0 transition cursor-pointer"
          title="Cuộn sang trái"
        >
          ‹
        </button>

        <!-- Scrollable Category container -->
        <div 
          ref="categoryScrollRef"
          @scroll="updateScrollButtons"
          class="flex items-center gap-1.5 overflow-x-auto no-scrollbar scroll-smooth py-1 min-w-0 flex-1 text-xs"
        >
          <!-- Tất cả -->
          <button 
            type="button"
            @click="selectService('ALL')"
            :class="[
              jobsStore.selectedService === 'ALL'
                ? 'bg-slate-900 text-white font-bold border-slate-900 shadow-xs ring-2 ring-slate-400'
                : 'bg-white text-slate-700 hover:bg-slate-50 border-slate-200 font-semibold shadow-2xs'
            ]"
            class="px-3 py-1.5 rounded-full border transition whitespace-nowrap flex items-center gap-1.5 shrink-0 cursor-pointer active:scale-95"
          >
            <span>✨</span>
            <span>Tất cả</span>
          </button>

          <!-- Dynamic services -->
          <button 
            v-for="svc in serviceList" 
            :key="svc.code"
            type="button"
            @click="selectService(svc.code)"
            :class="[
              jobsStore.selectedService === svc.code
                ? 'bg-blue-600 text-white font-bold border-blue-600 shadow-xs ring-2 ring-blue-300'
                : 'bg-white text-slate-700 hover:bg-slate-50 border-slate-200 font-semibold shadow-2xs'
            ]"
            class="px-3 py-1.5 rounded-full border transition whitespace-nowrap flex items-center gap-1.5 shrink-0 cursor-pointer active:scale-95"
          >
            <span>{{ svc.icon }}</span>
            <span>{{ svc.name }}</span>
          </button>
        </div>

        <!-- Right scroll arrow button (Outside, static flex item, NEVER overlaps pills!) -->
        <button 
          v-if="canScrollRight"
          type="button"
          @click="scrollCategories('right')"
          class="w-7 h-7 rounded-full bg-white border border-slate-200 shadow-sm hover:bg-slate-100 active:scale-95 flex items-center justify-center text-slate-700 font-bold text-xs shrink-0 transition cursor-pointer"
          title="Cuộn sang phải"
        >
          ›
        </button>
      </div>

    </div>

    <!-- RIGHT CLUSTER: DATE & MONTH FILTER (SHRINK-0: ALWAYS 100% VISIBLE, NEVER CUT OFF) -->
    <div class="shrink-0 pointer-events-auto pl-1">
      <div class="bg-white/98 backdrop-blur-md border border-slate-200/90 rounded-full shadow-md p-1 flex items-center gap-1.5 select-none text-xs">
        
        <!-- 1. Option Xem Cả Tháng -->
        <div class="flex items-center gap-0.5">
          <button 
            type="button"
            @click="toggleAllMonth(true)"
            :class="[
              jobsStore.viewAllMonth
                ? 'bg-blue-600 text-white font-bold shadow-xs'
                : 'text-slate-600 hover:bg-slate-100 font-semibold'
            ]"
            class="px-2.5 py-1.5 rounded-full text-xs transition flex items-center gap-1.5 cursor-pointer active:scale-95"
            title="Xem toàn bộ job trong tháng"
          >
            <span>🗓️</span>
            <span>Cả tháng {{ monthDisplay }}</span>
          </button>

          <!-- Nút chuyển tháng trước/sau khi xem cả tháng -->
          <button 
            v-if="jobsStore.viewAllMonth"
            type="button" 
            @click="prevMonth"
            class="w-6 h-6 rounded-full hover:bg-slate-100 flex items-center justify-center text-xs font-bold text-slate-600 cursor-pointer"
            title="Tháng trước"
          >
            ‹
          </button>
          <button 
            v-if="jobsStore.viewAllMonth"
            type="button" 
            @click="nextMonth"
            class="w-6 h-6 rounded-full hover:bg-slate-100 flex items-center justify-center text-xs font-bold text-slate-600 cursor-pointer"
            title="Tháng sau"
          >
            ›
          </button>
        </div>

        <div class="h-4 w-px bg-slate-200"></div>

        <!-- 2. Nút Hôm nay -->
        <button 
          type="button" 
          @click="setToday" 
          :class="[
            isTodaySelected && !jobsStore.viewAllMonth
              ? 'bg-blue-50 text-blue-700 border border-blue-200 font-bold'
              : 'text-slate-600 hover:bg-slate-100 font-semibold'
          ]"
          class="px-2.5 py-1.5 rounded-full text-xs transition cursor-pointer active:scale-95"
          title="Xem lịch hôm nay"
        >
          Hôm nay
        </button>

        <!-- 3. Nút Date Picker -->
        <div class="relative">
          <button 
            type="button"
            @click="triggerDatePicker"
            :class="[
              !jobsStore.viewAllMonth
                ? 'bg-slate-900 text-white font-bold shadow-xs ring-2 ring-slate-400'
                : 'bg-slate-50 hover:bg-slate-100 text-slate-700 border border-slate-200 font-semibold'
            ]"
            class="px-3 py-1.5 rounded-full text-xs transition flex items-center gap-1.5 cursor-pointer active:scale-95"
            title="Bấm để mở lịch chọn ngày cụ thể"
          >
            <span>📅</span>
            <span>{{ formatDateVN(jobsStore.selectedDate) }}</span>
            <span class="text-[9px] opacity-70">▼</span>
          </button>

          <!-- Hidden native date input -->
          <input 
            ref="datePickerInputRef"
            type="date" 
            :value="jobsStore.selectedDate"
            @change="onDatePicked"
            class="sr-only opacity-0 absolute inset-0 pointer-events-none"
            tabindex="-1"
          />
        </div>

      </div>
    </div>

  </header>
</template>

<style scoped>
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}
</style>
