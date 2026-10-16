<script setup lang="ts">
import { ref, watch } from 'vue';
import * as XLSX from 'xlsx';
import { useAuthStore } from '../stores/auth';
import { useJobsStore } from '../stores/jobs';
import { SERVICE_CATALOG } from '../types';

const emit = defineEmits<{
  (e: 'open-profile'): void;
  (e: 'open-login'): void;
  (e: 'fly-to', coords: { lat: number; lng: number; mainName: string; subAddress: string }): void;
}>();

const authStore = useAuthStore();
const jobsStore = useJobsStore();

const searchQuery = ref('');
const searchResults = ref<{ mainName: string; subAddress: string; lat: number; lng: number }[]>([]);
const isSearching = ref(false);
let searchDebounceTimeout: any = null;

function onSearchInput() {
  clearTimeout(searchDebounceTimeout);
  if (!searchQuery.value.trim()) {
    searchResults.value = [];
    return;
  }
  searchDebounceTimeout = setTimeout(searchPlaces, 350);
}

async function searchPlaces() {
  const q = searchQuery.value.trim();
  if (!q) return;
  isSearching.value = true;
  try {
    // Focus around Hanoi center by default
    const url = `https://photon.komoot.io/api/?q=${encodeURIComponent(q)}&lat=21.0285&lon=105.8542&limit=5`;
    const res = await fetch(url).then(r => r.json());
    if (res && res.features) {
      searchResults.value = res.features.map((f: any) => ({
        mainName: f.properties.name || f.properties.street || 'Địa điểm',
        subAddress: [f.properties.street, f.properties.district, f.properties.city].filter(Boolean).join(', '),
        lat: f.geometry.coordinates[1],
        lng: f.geometry.coordinates[0],
      }));
    }
  } catch (err) {
    console.error('Photon search failed', err);
  } finally {
    isSearching.value = false;
  }
}

function pickSearchResult(place: { mainName: string; subAddress: string; lat: number; lng: number }) {
  searchResults.value = [];
  searchQuery.value = place.mainName;
  emit('fly-to', place);
}

function clearSearch() {
  searchQuery.value = '';
  searchResults.value = [];
}

// Watch date / month filters
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
  const s = SERVICE_CATALOG.find(x => x.code === code);
  return s ? `${s.icon} ${s.name}` : code;
}

function formatDateVN(dateStr: string) {
  if (!dateStr) return '';
  return dateStr.split('-').reverse().join('/');
}

// SheetJS Excel Export
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

  // Column width formatting
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
</script>

<template>
  <header class="absolute top-3 left-3 right-3 z-20 flex flex-wrap items-center justify-between gap-2.5 pointer-events-none">
    
    <!-- LEFT CLUSTER: Place Search + Date/Month Filter -->
    <div class="flex items-center gap-2 pointer-events-auto">

      <!-- Place Search Autocomplete -->
      <div class="relative w-64 sm:w-80">
        <div class="bg-white/95 backdrop-blur-md border border-slate-200/90 shadow-lg rounded-2xl px-3 py-2 flex items-center gap-2 transition focus-within:ring-2 focus-within:ring-indigo-500/30 focus-within:border-indigo-500">
          <span class="text-slate-400 text-xs">🔍</span>
          <input 
            v-model="searchQuery" 
            @input="onSearchInput"
            @keyup.enter="searchPlaces"
            placeholder="Tìm địa điểm (VD: Trống Đồng, Sheraton)..."
            class="w-full bg-transparent text-xs font-medium text-slate-800 placeholder-slate-400 focus:outline-none"
          />
          <button 
            v-if="searchQuery" 
            @click="clearSearch"
            type="button"
            class="text-slate-400 hover:text-slate-600 text-xs px-1"
          >
            ✕
          </button>
        </div>

        <!-- Search Results Dropdown -->
        <div 
          v-if="searchResults.length > 0"
          class="absolute left-0 right-0 mt-1.5 bg-white/98 backdrop-blur-lg border border-slate-200 rounded-2xl shadow-2xl overflow-hidden max-h-64 overflow-y-auto divide-y divide-slate-100 z-50 animate-in fade-in slide-in-from-top-1"
        >
          <div 
            v-for="(p, idx) in searchResults" 
            :key="idx" 
            @click="pickSearchResult(p)"
            class="p-2.5 hover:bg-indigo-50/80 cursor-pointer transition text-xs"
          >
            <div class="font-bold text-slate-900 flex items-center gap-1.5">
              <span>📍</span>
              <span class="truncate">{{ p.mainName }}</span>
            </div>
            <div class="text-[11px] text-slate-500 truncate pl-5">
              {{ p.subAddress }}
            </div>
          </div>
        </div>
      </div>

      <!-- Date & Month Picker -->
      <div class="bg-white/95 backdrop-blur-md border border-slate-200/90 shadow-lg rounded-2xl p-1 flex items-center gap-1">
        <input 
          type="date" 
          v-model="jobsStore.selectedDate"
          class="bg-transparent px-2.5 py-1 text-xs font-bold text-slate-800 focus:outline-none cursor-pointer"
          title="Chọn ngày cụ thể"
        />
        <button 
          @click="jobsStore.viewAllMonth = !jobsStore.viewAllMonth"
          type="button"
          :class="[
            jobsStore.viewAllMonth 
              ? 'bg-indigo-600 text-white shadow-xs' 
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
          ]"
          class="px-2.5 py-1 rounded-xl text-[11px] font-semibold transition"
        >
          {{ jobsStore.viewAllMonth ? 'Xem cả tháng' : 'Xem theo ngày' }}
        </button>
      </div>

    </div>

    <!-- RIGHT CLUSTER: Excel Export + Profile & User Switcher -->
    <div class="flex items-center gap-2 pointer-events-auto">

      <!-- Monthly Excel Export -->
      <div class="bg-white/95 backdrop-blur-md border border-slate-200/90 shadow-lg rounded-2xl p-1 flex items-center gap-1">
        <input 
          type="month" 
          v-model="jobsStore.selectedMonth"
          class="bg-transparent pl-2.5 pr-1 py-1 text-xs font-semibold text-slate-700 focus:outline-none cursor-pointer"
          title="Chọn tháng xuất file"
        />
        <button 
          @click="exportMonthlyExcel"
          type="button"
          class="bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-xl transition flex items-center gap-1.5 shadow-xs"
          title="Xuất bảng kê Excel công việc của bạn trong tháng"
        >
          <span>📥</span>
          <span class="hidden sm:inline">Xuất</span> Excel
        </button>
      </div>

      <!-- User Profile & Switcher -->
      <div class="bg-white/95 backdrop-blur-md border border-slate-200/90 shadow-lg rounded-2xl p-1 flex items-center gap-1.5">
        
        <!-- User Avatar & Switch Button -->
        <button
          @click="$emit('open-login')"
          type="button"
          class="flex items-center gap-2 pl-1 pr-2.5 py-0.5 rounded-xl hover:bg-slate-100 transition group"
          title="Bấm để đổi thành viên (Đăng nhập PIN)"
        >
          <div class="w-6 h-6 rounded-lg overflow-hidden bg-slate-100 border border-slate-200 shrink-0">
            <img 
              :src="authStore.currentUser?.avatarUrl || `https://api.dicebear.com/7.x/bottts/svg?seed=${authStore.currentUser?.name}`" 
              :alt="authStore.currentUser?.name"
              class="w-full h-full object-cover" 
            />
          </div>
          <span class="text-xs font-bold text-slate-800 group-hover:text-indigo-600">
            {{ authStore.currentUser?.name || 'Đăng nhập' }}
          </span>
          <span class="text-[10px] text-slate-400">⇄</span>
        </button>

        <!-- Settings Modal Button -->
        <button 
          @click="$emit('open-profile')"
          type="button"
          class="bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold px-2.5 py-1.5 rounded-xl transition flex items-center gap-1"
          title="Cài đặt kỹ năng & lịch bận hành chính"
        >
          <span>⚙️</span>
          <span class="hidden sm:inline">Profile</span>
        </button>

      </div>

    </div>

  </header>
</template>
