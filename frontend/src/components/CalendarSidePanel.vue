<script setup lang="ts">
import { ref, computed } from 'vue';
import { useJobsStore } from '../stores/jobs';
import { useAuthStore } from '../stores/auth';
import type { JobResponseDto } from '../types';
import { SERVICE_CATALOG } from '../types';

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'fly-to', coords: { lat: number; lng: number; mainName: string; subAddress: string }): void;
  (e: 'select-job', job: JobResponseDto): void;
}>();

const jobsStore = useJobsStore();
const authStore = useAuthStore();

const panelTab = ref<'day' | 'month'>('day');

// Date management
const selectedDate = computed({
  get: () => jobsStore.selectedDate,
  set: (val: string) => {
    jobsStore.selectedDate = val;
    jobsStore.viewAllMonth = false;
    jobsStore.loadJobs();
  }
});

function prevDay() {
  const d = new Date(jobsStore.selectedDate);
  d.setDate(d.getDate() - 1);
  setDateFromObj(d);
}

function nextDay() {
  const d = new Date(jobsStore.selectedDate);
  d.setDate(d.getDate() + 1);
  setDateFromObj(d);
}

function goToday() {
  const now = new Date();
  setDateFromObj(now);
}

function setDateFromObj(d: Date) {
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  jobsStore.selectedDate = `${yyyy}-${mm}-${dd}`;
  jobsStore.selectedMonth = `${yyyy}-${mm}`;
  jobsStore.viewAllMonth = false;
  jobsStore.loadJobs();
}

// Jobs for the active date (filtered by selected service if any)
const dayJobs = computed(() => {
  const filterSvc = jobsStore.selectedService?.trim().toUpperCase();
  return jobsStore.jobs
    .filter(j => {
      if (j.eventDate !== jobsStore.selectedDate) return false;
      if (filterSvc && filterSvc !== 'ALL') {
        if (j.serviceCode?.trim().toUpperCase() !== filterSvc) return false;
      }
      return true;
    })
    .sort((a, b) => a.startTime.localeCompare(b.startTime));
});

// Grouped jobs for the active month (filtered by selected service if any)
const monthGroupedJobs = computed(() => {
  const filterSvc = jobsStore.selectedService?.trim().toUpperCase();
  const map = new Map<string, JobResponseDto[]>();
  jobsStore.jobs.forEach(j => {
    if (!j.eventDate.startsWith(jobsStore.selectedMonth)) return;
    if (filterSvc && filterSvc !== 'ALL') {
      if (j.serviceCode?.trim().toUpperCase() !== filterSvc) return;
    }
    if (!map.has(j.eventDate)) {
      map.set(j.eventDate, []);
    }
    map.get(j.eventDate)!.push(j);
  });

  const list: { dateStr: string; displayDate: string; count: number; jobs: JobResponseDto[] }[] = [];
  Array.from(map.keys()).sort().forEach(dateStr => {
    const jobs = map.get(dateStr)!.sort((a, b) => a.startTime.localeCompare(b.startTime));
    list.push({
      dateStr,
      displayDate: formatDateVN(dateStr),
      count: jobs.length,
      jobs
    });
  });

  return list;
});

function formatDateVN(dateStr: string) {
  if (!dateStr) return '';
  const parts = dateStr.split('-');
  if (parts.length !== 3) return dateStr;
  return `${parts[2]}/${parts[1]}/${parts[0]}`;
}

function getServiceInfo(code: string) {
  const list = authStore.services.length > 0 ? authStore.services : SERVICE_CATALOG;
  return list.find(s => s.code === code) || { icon: '📍', name: code };
}

function handleJobClick(job: JobResponseDto) {
  emit('fly-to', {
    lat: job.latitude,
    lng: job.longitude,
    mainName: job.locationName,
    subAddress: job.subAddress
  });
  emit('select-job', job);
}

function openEditJob(job: JobResponseDto) {
  jobsStore.openEdit(job);
}
</script>

<template>
  <aside 
    class="absolute top-0 bottom-0 left-16 sm:left-[68px] z-25 w-96 sm:w-[410px] max-w-[calc(100vw-72px)] bg-white border-r border-slate-200/90 shadow-2xl flex flex-col select-none transition-all animate-in slide-in-from-left duration-250"
    aria-label="Google Maps Schedule Side Panel"
  >
    <!-- TOP HEADER -->
    <div class="p-4 border-b border-slate-100 flex items-center justify-between shrink-0">
      <div class="flex items-center gap-2.5">
        <div class="w-8 h-8 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center font-bold text-lg">
          📅
        </div>
        <div>
          <h2 class="text-sm font-extrabold text-slate-900 tracking-tight">Lịch Trình & Show</h2>
          <div class="text-[11px] text-slate-500 font-medium">
            Tháng {{ jobsStore.selectedMonth }}
          </div>
        </div>
      </div>

      <button 
        type="button" 
        @click="$emit('close')"
        class="w-8 h-8 rounded-full hover:bg-slate-100 text-slate-400 hover:text-slate-700 flex items-center justify-center text-sm font-bold transition cursor-pointer"
        title="Đóng bảng lịch trình"
      >
        ✕
      </button>
    </div>

    <!-- TABS BAR: 2 TABS ONLY (THEO NGÀY & CẢ THÁNG) -->
    <div class="px-4 pt-2.5 pb-2 border-b border-slate-100 flex items-center shrink-0 text-xs font-semibold">
      <div class="flex items-center bg-slate-100 p-0.5 rounded-xl w-full">
        <button 
          type="button" 
          @click="panelTab = 'day'"
          :class="panelTab === 'day' ? 'bg-white text-blue-700 font-bold shadow-2xs' : 'text-slate-600 hover:text-slate-900'"
          class="flex-1 py-1.5 rounded-lg transition text-center cursor-pointer"
        >
          Theo ngày
        </button>
        <button 
          type="button" 
          @click="panelTab = 'month'"
          :class="panelTab === 'month' ? 'bg-white text-blue-700 font-bold shadow-2xs' : 'text-slate-600 hover:text-slate-900'"
          class="flex-1 py-1.5 rounded-lg transition text-center cursor-pointer"
        >
          Cả tháng
        </button>
      </div>
    </div>

    <!-- CONTENT: TAB 1 - THEO NGÀY (DAY TIMELINE) -->
    <div v-if="panelTab === 'day'" class="flex-1 min-h-0 flex flex-col">
      
      <!-- Date Navigator Bar -->
      <div class="px-4 py-3 bg-slate-50/80 border-b border-slate-100 flex items-center justify-between gap-2 shrink-0">
        <div class="flex items-center gap-1">
          <button 
            type="button" 
            @click="prevDay"
            class="w-7 h-7 rounded-lg hover:bg-white text-slate-600 flex items-center justify-center font-bold text-xs transition border border-transparent hover:border-slate-200"
            title="Ngày trước"
          >
            ‹
          </button>
          <span class="text-xs font-extrabold text-slate-800 px-1">
            {{ formatDateVN(jobsStore.selectedDate) }}
          </span>
          <button 
            type="button" 
            @click="nextDay"
            class="w-7 h-7 rounded-lg hover:bg-white text-slate-600 flex items-center justify-center font-bold text-xs transition border border-transparent hover:border-slate-200"
            title="Ngày sau"
          >
            ›
          </button>
        </div>

        <div class="flex items-center gap-1.5">
          <button 
            type="button" 
            @click="goToday"
            class="px-2.5 py-1 rounded-lg text-[11px] font-bold text-blue-700 bg-blue-50 hover:bg-blue-100 transition"
          >
            Hôm nay
          </button>
          <input 
            type="date" 
            v-model="selectedDate" 
            class="w-6 h-6 opacity-0 absolute pointer-events-none"
            id="panel-date-picker"
          />
          <label 
            for="panel-date-picker" 
            class="cursor-pointer text-slate-500 hover:text-slate-800 text-xs px-1"
            title="Chọn ngày khác"
          >
            📅
          </label>
        </div>
      </div>

      <!-- Day Show List -->
      <div class="flex-1 min-h-0 overflow-y-auto p-4 space-y-3">
        <div 
          v-if="dayJobs.length === 0" 
          class="h-56 flex flex-col items-center justify-center text-center text-slate-400"
        >
          <div class="text-3xl mb-2">🏖️</div>
          <div class="text-xs font-bold text-slate-600">Không có show nào ngày này</div>
          <div class="text-[11px] text-slate-400 mt-1">
            Click lên bản đồ để tạo show hoặc chọn ngày khác
          </div>
        </div>

        <div 
          v-for="job in dayJobs" 
          :key="job.id"
          class="bg-white border border-slate-200 hover:border-blue-400 rounded-2xl p-3.5 shadow-2xs hover:shadow-md transition cursor-pointer group"
          @click="handleJobClick(job)"
        >
          <!-- Top Row: Time & Service Badge -->
          <div class="flex items-center justify-between gap-2 mb-2">
            <div class="flex items-center gap-1.5 bg-slate-100 px-2.5 py-1 rounded-full text-xs font-extrabold text-slate-800">
              <span>⏰</span>
              <span>{{ job.startTime }} - {{ job.endTime }}</span>
            </div>

            <span class="text-xs font-bold px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 border border-blue-100 flex items-center gap-1">
              <span>{{ getServiceInfo(job.serviceCode).icon }}</span>
              <span>{{ getServiceInfo(job.serviceCode).name }}</span>
            </span>
          </div>

          <!-- Location Name (Job Title) -->
          <h3 class="font-extrabold text-slate-900 text-sm group-hover:text-blue-600 transition flex items-center gap-1.5">
            <span class="text-blue-600">📍</span>
            <span class="truncate">{{ job.locationName }}</span>
          </h3>

          <!-- Sub Address -->
          <p v-if="job.subAddress" class="text-[11px] text-slate-500 truncate pl-5 mt-0.5">
            {{ job.subAddress }}
          </p>

          <!-- Crew Members & Payout Info -->
          <div class="mt-3 pt-2.5 border-t border-slate-100 flex items-center justify-between text-xs">
            <div class="text-[11px] text-slate-600 flex items-center gap-1 truncate max-w-[65%]">
              <span>👥</span>
              <span class="font-semibold truncate">
                {{ job.assignments.map(a => a.userName).join(', ') || 'Chưa phân thợ' }}
              </span>
            </div>

            <div class="flex items-center gap-1">
              <button 
                type="button" 
                @click.stop="openEditJob(job)"
                class="px-2 py-1 rounded-lg text-[10px] font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 transition"
                title="Xem / Chỉnh sửa chi tiết"
              >
                Chi tiết ✏️
              </button>
            </div>
          </div>
        </div>
      </div>

    </div>

    <!-- CONTENT: TAB 2 - CẢ THÁNG (MONTH OVERVIEW) -->
    <div v-else class="flex-1 min-h-0 overflow-y-auto p-4 space-y-4">
      <div 
        v-if="monthGroupedJobs.length === 0" 
        class="h-56 flex flex-col items-center justify-center text-center text-slate-400"
      >
        <div class="text-3xl mb-2">🗓️</div>
        <div class="text-xs font-bold text-slate-600">Tháng này chưa có show nào</div>
      </div>

      <div 
        v-for="group in monthGroupedJobs" 
        :key="group.dateStr"
        class="bg-slate-50/70 border border-slate-200/80 rounded-2xl p-3"
      >
        <!-- Group Header -->
        <div class="flex items-center justify-between pb-2 mb-2 border-b border-slate-200">
          <div class="flex items-center gap-2">
            <span class="text-xs font-extrabold text-slate-800">
              📅 {{ group.displayDate }}
            </span>
            <span class="text-[10px] font-extrabold px-2 py-0.5 bg-blue-100 text-blue-800 rounded-full">
              {{ group.count }} show
            </span>
          </div>

          <button 
            type="button" 
            @click="jobsStore.selectedDate = group.dateStr; panelTab = 'day'"
            class="text-[11px] font-bold text-blue-600 hover:text-blue-800 cursor-pointer"
          >
            Xem ngày →
          </button>
        </div>

        <!-- Jobs in date -->
        <div class="space-y-1.5">
          <div 
            v-for="job in group.jobs" 
            :key="job.id"
            @click="handleJobClick(job)"
            class="p-2 bg-white rounded-xl border border-slate-100 hover:border-blue-300 hover:shadow-xs transition cursor-pointer flex items-center justify-between gap-2 text-xs"
          >
            <div class="min-w-0 flex-1 flex items-center gap-2">
              <span class="font-extrabold text-slate-700 shrink-0 text-[11px]">{{ job.startTime }}</span>
              <span class="shrink-0">{{ getServiceInfo(job.serviceCode).icon }}</span>
              <span class="font-bold text-slate-800 truncate">{{ job.locationName }}</span>
            </div>

            <span class="text-[10px] text-slate-400 font-medium shrink-0">
              📍 Map
            </span>
          </div>
        </div>
      </div>
    </div>

  </aside>
</template>
