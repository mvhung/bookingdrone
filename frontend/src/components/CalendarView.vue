<script setup lang="ts">
import { ref, computed } from 'vue';
import { useJobsStore } from '../stores/jobs';
import { useAuthStore } from '../stores/auth';
import type { JobResponseDto, BusyDateItemDto } from '../types';

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const jobsStore = useJobsStore();
const authStore = useAuthStore();

// Calendar Navigation
const currentDate = ref(new Date(2026, 9, 1)); // Default Oct 2026

const currentYear = computed(() => currentDate.value.getFullYear());
const currentMonth = computed(() => currentDate.value.getMonth()); // 0-indexed

const monthYearTitle = computed(() => {
  const month = currentMonth.value + 1;
  return `Tháng ${month.toString().padStart(2, '0')} / ${currentYear.value}`;
});

function prevMonth() {
  currentDate.value = new Date(currentYear.value, currentMonth.value - 1, 1);
  syncJobsStoreMonth();
}

function nextMonth() {
  currentDate.value = new Date(currentYear.value, currentMonth.value + 1, 1);
  syncJobsStoreMonth();
}

function goToday() {
  currentDate.value = new Date(2026, 9, 18); // Default Oct 18, 2026 or real current date
  syncJobsStoreMonth();
}

function syncJobsStoreMonth() {
  const m = (currentMonth.value + 1).toString().padStart(2, '0');
  jobsStore.selectedMonth = `${currentYear.value}-${m}`;
  jobsStore.loadJobs();
}

// Filter mode: 'all' or 'mine'
const filterMode = ref<'all' | 'mine'>('all');

// Days of week header (Monday first)
const weekDays = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'Chủ Nhật'];

interface CalendarDay {
  date: Date;
  dateStr: string; // YYYY-MM-DD
  dayNumber: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  isWeekend: boolean;
  isWeekday: boolean;
  jobs: JobResponseDto[];
  busyDates: BusyDateItemDto[];
  hasOfficeHours: boolean;
}

const calendarDays = computed<CalendarDay[]>(() => {
  const year = currentYear.value;
  const month = currentMonth.value;

  // First day of month
  const firstDay = new Date(year, month, 1);
  // Last day of month
  const lastDay = new Date(year, month + 1, 0);

  // Day of week for first day (0 is Sun, 1 is Mon, etc.)
  // We want Mon as 0, Sun as 6
  let firstDayIndex = firstDay.getDay() - 1;
  if (firstDayIndex === -1) firstDayIndex = 6;

  const days: CalendarDay[] = [];

  // Previous month trailing days
  const prevLastDay = new Date(year, month, 0).getDate();
  for (let i = firstDayIndex - 1; i >= 0; i--) {
    const d = new Date(year, month - 1, prevLastDay - i);
    days.push(createDayObject(d, false));
  }

  // Current month days
  for (let i = 1; i <= lastDay.getDate(); i++) {
    const d = new Date(year, month, i);
    days.push(createDayObject(d, true));
  }

  // Next month leading days to complete 35 or 42 cells
  const remaining = (7 - (days.length % 7)) % 7;
  for (let i = 1; i <= remaining; i++) {
    const d = new Date(year, month + 1, i);
    days.push(createDayObject(d, false));
  }

  return days;
});

function createDayObject(d: Date, isCurrentMonth: boolean): CalendarDay {
  const y = d.getFullYear();
  const m = (d.getMonth() + 1).toString().padStart(2, '0');
  const day = d.getDate().toString().padStart(2, '0');
  const dateStr = `${y}-${m}-${day}`;

  const dayOfWeek = d.getDay(); // 0 Sun, 6 Sat
  const isWeekend = dayOfWeek === 0 || dayOfWeek === 6;
  const isWeekday = !isWeekend;

  // Jobs matching this date
  const currentUserId = authStore.currentUser?.id;
  const dayJobs = jobsStore.jobs.filter(j => {
    if (j.eventDate !== dateStr) return false;
    if (filterMode.value === 'mine') {
      return j.ownerUserId === currentUserId || j.assignments.some(a => a.userId === currentUserId);
    }
    return true;
  });

  // Busy dates matching this date
  const busyDates = (authStore.currentUser?.customBusyDates || []).filter(b => b.date === dateStr);

  // Office hours
  const hasOfficeHours = !!authStore.currentUser?.officeBusyEnabled && isWeekday;

  // Today check (using Oct 18, 2026 or real today)
  const isToday = dateStr === '2026-10-18';

  return {
    date: d,
    dateStr,
    dayNumber: d.getDate(),
    isCurrentMonth,
    isToday,
    isWeekend,
    isWeekday,
    jobs: dayJobs,
    busyDates,
    hasOfficeHours,
  };
}

// Quick Busy Modal State
const showAddBusyModal = ref(false);
const busyForm = ref({
  date: '',
  isAllDay: true,
  startTime: '08:00',
  endTime: '17:30',
  reason: 'Việc riêng',
});

const quickReasons = ['Việc riêng', 'Đi du lịch', 'Khám bệnh', 'Việc gia đình', 'Bận việc cá nhân', 'Về quê'];

function openAddBusy(prefilledDate?: string) {
  busyForm.value = {
    date: prefilledDate || jobsStore.selectedDate || '2026-10-18',
    isAllDay: true,
    startTime: '08:00',
    endTime: '17:30',
    reason: 'Việc riêng',
  };
  showAddBusyModal.value = true;
}

const isSavingBusy = ref(false);

async function saveBusyDate() {
  if (!busyForm.value.date) return;
  isSavingBusy.value = true;
  try {
    const start = busyForm.value.isAllDay ? undefined : busyForm.value.startTime;
    const end = busyForm.value.isAllDay ? undefined : busyForm.value.endTime;
    await authStore.addBusyDate(busyForm.value.date, busyForm.value.reason, start, end);
    showAddBusyModal.value = false;
  } catch (err: any) {
    alert(err.message || 'Lỗi khi lưu lịch bận');
  } finally {
    isSavingBusy.value = false;
  }
}

async function removeBusyDate(id: number) {
  if (confirm('Bạn có chắc muốn xóa lịch bận này?')) {
    try {
      await authStore.removeBusyDate(id);
    } catch (err: any) {
      alert(err.message || 'Lỗi khi xóa lịch bận');
    }
  }
}

function openJobDetail(job: JobResponseDto) {
  jobsStore.activeJob = job;
  jobsStore.drawerMode = 'edit';
  jobsStore.drawerOpen = true;
}

function getServiceIcon(code: string) {
  const sv = authStore.services.find(s => s.code === code);
  return sv ? sv.icon : '🎯';
}


</script>

<template>
  <div class="fixed inset-0 z-40 bg-slate-950/80 backdrop-blur-md flex flex-col overflow-hidden animate-in fade-in duration-200">
    
    <!-- TOP TOOLBAR -->
    <header class="bg-white/95 backdrop-blur-md border-b border-slate-200 px-4 py-3 flex flex-wrap items-center justify-between gap-3 shrink-0 shadow-xs">
      
      <!-- Left: Title & Month Navigation -->
      <div class="flex items-center gap-3">
        <div class="flex items-center gap-2">
          <span class="text-2xl">📅</span>
          <div>
            <h2 class="text-base font-extrabold text-slate-900 tracking-tight flex items-center gap-2">
              Lịch Biểu & Quản Lý Lịch Bận
            </h2>
            <p class="text-[11px] text-slate-500 font-medium">
              Lịch chạy job của {{ authStore.currentUser?.name }} & danh sách ngày báo nghỉ
            </p>
          </div>
        </div>

        <!-- Month Switcher Controls -->
        <div class="flex items-center bg-slate-100 p-1 rounded-2xl border border-slate-200 ml-2">
          <button 
            @click="prevMonth"
            class="w-7 h-7 rounded-xl hover:bg-white text-slate-700 flex items-center justify-center font-bold text-xs transition shadow-xs"
            title="Tháng trước"
          >
            ◀
          </button>
          <span class="px-3 font-bold text-xs text-slate-800 min-w-[130px] text-center">
            {{ monthYearTitle }}
          </span>
          <button 
            @click="nextMonth"
            class="w-7 h-7 rounded-xl hover:bg-white text-slate-700 flex items-center justify-center font-bold text-xs transition shadow-xs"
            title="Tháng sau"
          >
            ▶
          </button>
        </div>

        <button 
          @click="goToday"
          class="px-3 py-1.5 rounded-xl border border-slate-200 bg-white hover:bg-slate-50 font-semibold text-xs text-slate-700 transition shadow-xs"
        >
          Hôm nay
        </button>
      </div>

      <!-- Center: Filter & Legend -->
      <div class="hidden lg:flex items-center gap-4 text-xs font-medium text-slate-600">
        <div class="flex items-center gap-1.5">
          <span class="w-3 h-3 rounded-full bg-indigo-600"></span>
          <span>Công việc (Job)</span>
        </div>
        <div class="flex items-center gap-1.5">
          <span class="w-3 h-3 rounded-full bg-rose-500"></span>
          <span>Lịch báo nghỉ</span>
        </div>
        <div v-if="authStore.currentUser?.officeBusyEnabled" class="flex items-center gap-1.5">
          <span class="w-3 h-3 rounded-full bg-amber-400"></span>
          <span>Giờ hành chính (T2-T6)</span>
        </div>
      </div>

      <!-- Right: Action Buttons -->
      <div class="flex items-center gap-2">


        <!-- Add Busy Date Button -->
        <button 
          @click="openAddBusy()"
          class="px-3.5 py-1.5 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-bold text-xs flex items-center gap-1.5 shadow-sm transition active:scale-95"
        >
          <span>➕</span>
          <span>Báo Nghỉ / Bận</span>
        </button>

        <!-- Close / Back to Map -->
        <button 
          @click="$emit('close')"
          class="px-3.5 py-1.5 rounded-xl bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs flex items-center gap-1.5 shadow-sm transition"
        >
          <span>✕</span>
          <span>Bản Đồ</span>
        </button>
      </div>

    </header>

    <!-- CALENDAR GRID CONTAINER (with min-h-0 and generous bottom padding) -->
    <div class="flex-1 min-h-0 bg-slate-100 p-2 sm:p-4 pb-36 overflow-y-auto">
      <div class="max-w-7xl mx-auto bg-white rounded-3xl shadow-xl border border-slate-200/90 overflow-hidden flex flex-col min-h-full mb-16">
        
        <!-- WEEKDAYS HEADER (7 cols) -->
        <div class="grid grid-cols-7 border-b border-slate-200 bg-slate-50/80 text-center text-xs font-bold text-slate-600 divide-x divide-slate-200 shrink-0">
          <div 
            v-for="(dayName, idx) in weekDays" 
            :key="idx" 
            class="py-2.5 uppercase tracking-wider"
            :class="idx >= 5 ? 'text-indigo-600 bg-indigo-50/30' : ''"
          >
            {{ dayName }}
          </div>
        </div>

        <!-- DAYS CELLS GRID (7 cols) -->
        <div class="grid grid-cols-7 flex-1 divide-x divide-y divide-slate-100 min-h-[650px] auto-rows-fr">
          <div 
            v-for="(day, dIdx) in calendarDays" 
            :key="dIdx"
            class="p-1.5 sm:p-2 min-h-[110px] flex flex-col transition hover:bg-slate-50/60 relative group"
            :class="[
              !day.isCurrentMonth ? 'bg-slate-50/40 text-slate-400' : 'bg-white',
              day.isToday ? 'bg-indigo-50/20' : ''
            ]"
          >
            <!-- Cell Header: Day Number + Quick Add -->
            <div class="flex items-center justify-between mb-1">
              <span 
                class="w-6 h-6 rounded-full flex items-center justify-center text-xs font-extrabold transition"
                :class="[
                  day.isToday 
                    ? 'bg-indigo-600 text-white shadow-xs' 
                    : day.isCurrentMonth ? 'text-slate-800' : 'text-slate-400'
                ]"
              >
                {{ day.dayNumber }}
              </span>

              <!-- Hover '+' button to add busy date on this specific day -->
              <button 
                @click.stop="openAddBusy(day.dateStr)"
                type="button"
                class="opacity-0 group-hover:opacity-100 w-5 h-5 rounded-full bg-slate-100 hover:bg-rose-100 hover:text-rose-700 text-slate-500 text-[10px] flex items-center justify-center font-bold transition"
                title="Báo nghỉ cho ngày này"
              >
                +
              </button>
            </div>

            <!-- EVENTS / PILLS CONTAINER -->
            <div class="flex-1 space-y-1 overflow-y-auto max-h-[130px] pr-0.5 text-[11px]">
              
              <!-- 1. Office Hours Strip (Mon-Fri) -->
              <div 
                v-if="day.hasOfficeHours"
                class="px-1.5 py-0.5 rounded-lg bg-amber-50 text-amber-800 border border-amber-200/80 font-semibold truncate flex items-center gap-1 text-[10px]"
                title="Bận giờ hành chính cố định"
              >
                <span>🏢</span>
                <span class="truncate">{{ authStore.currentUser?.officeBusyStart }} - {{ authStore.currentUser?.officeBusyEnd }} HC</span>
              </div>

              <!-- 2. Busy Date / Day Off Pills -->
              <div 
                v-for="b in day.busyDates" 
                :key="b.id"
                class="px-1.5 py-0.5 rounded-lg bg-rose-50 text-rose-700 border border-rose-200 font-bold truncate flex items-center justify-between gap-1 group/busy text-[10px]"
              >
                <div class="truncate flex items-center gap-1">
                  <span>🚫</span>
                  <span class="truncate">
                    <span v-if="b.startTime && b.endTime">{{ b.startTime }}-{{ b.endTime }}</span>
                    {{ b.reason || 'Báo nghỉ' }}
                  </span>
                </div>
                <button 
                  @click.stop="removeBusyDate(b.id)"
                  type="button"
                  class="opacity-0 group-hover/busy:opacity-100 hover:text-rose-900 font-extrabold px-1 text-[10px]"
                  title="Xóa lịch bận"
                >
                  ✕
                </button>
              </div>

              <!-- 3. Event Jobs -->
              <div 
                v-for="j in day.jobs" 
                :key="j.id"
                @click.stop="openJobDetail(j)"
                class="px-2 py-1 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-semibold cursor-pointer shadow-xs transition hover:scale-[1.02] flex items-center justify-between gap-1 group/job truncate"
                :title="`${j.startTime} - ${j.endTime} • ${j.locationName}`"
              >
                <div class="flex items-center gap-1.5 truncate">
                  <span>{{ getServiceIcon(j.serviceCode) }}</span>
                  <span class="text-[10px] font-bold shrink-0">{{ j.startTime }}</span>
                  <span class="truncate font-medium">{{ j.locationName }}</span>
                </div>
              </div>

            </div>

          </div>
        </div>

      </div>
    </div>

    <!-- QUICK ADD BUSY MODAL -->
    <div 
      v-if="showAddBusyModal" 
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm"
      @click.self="showAddBusyModal = false"
    >
      <div class="relative w-full max-w-sm bg-white border border-slate-200 shadow-2xl rounded-3xl p-5 space-y-4 animate-in zoom-in-95 duration-150">
        
        <div class="flex items-center justify-between border-b border-slate-100 pb-3">
          <div class="flex items-center gap-2">
            <span class="text-xl">📅</span>
            <div>
              <h3 class="font-bold text-sm text-slate-900">Báo Bận / Nghỉ Phép</h3>
              <p class="text-[11px] text-slate-400">Đăng ký ngày hoặc giờ bạn không nhận chạy job</p>
            </div>
          </div>
          <button 
            @click="showAddBusyModal = false"
            class="w-7 h-7 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 font-bold text-xs"
          >
            ✕
          </button>
        </div>

        <form @submit.prevent="saveBusyDate" class="space-y-3 text-xs">
          <!-- Date Input -->
          <div>
            <label class="block font-bold text-slate-700 mb-1">Ngày báo nghỉ</label>
            <input 
              type="date" 
              v-model="busyForm.date"
              required
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-bold text-slate-800 focus:outline-none focus:ring-2 focus:ring-rose-500/30"
            />
          </div>

          <!-- All Day Toggle -->
          <label class="flex items-center gap-2 cursor-pointer bg-slate-50 p-2.5 rounded-xl border border-slate-200">
            <input 
              type="checkbox" 
              v-model="busyForm.isAllDay"
              class="w-4 h-4 text-rose-600 rounded cursor-pointer"
            />
            <span class="font-bold text-slate-800">Nghỉ cả ngày</span>
          </label>

          <!-- Custom Time Range if not all day -->
          <div v-if="!busyForm.isAllDay" class="flex items-center gap-2 bg-amber-50/60 p-2.5 rounded-xl border border-amber-200">
            <div class="flex-1">
              <label class="block text-[10px] font-bold text-amber-800 mb-0.5">Từ giờ</label>
              <input 
                type="time" 
                v-model="busyForm.startTime"
                class="w-full bg-white border border-amber-300 rounded-lg px-2 py-1 font-bold text-slate-800 text-center"
              />
            </div>
            <span class="text-amber-600 font-bold pt-4">➔</span>
            <div class="flex-1">
              <label class="block text-[10px] font-bold text-amber-800 mb-0.5">Đến giờ</label>
              <input 
                type="time" 
                v-model="busyForm.endTime"
                class="w-full bg-white border border-amber-300 rounded-lg px-2 py-1 font-bold text-slate-800 text-center"
              />
            </div>
          </div>

          <!-- Quick Reason Chips -->
          <div>
            <label class="block font-bold text-slate-700 mb-1.5">Lý do nghỉ</label>
            <div class="flex flex-wrap gap-1 mb-2">
              <button 
                v-for="r in quickReasons" 
                :key="r"
                type="button"
                @click="busyForm.reason = r"
                class="px-2 py-1 rounded-lg text-[10px] font-semibold border transition"
                :class="busyForm.reason === r ? 'bg-rose-600 text-white border-rose-600' : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'"
              >
                {{ r }}
              </button>
            </div>
            <input 
              type="text" 
              v-model="busyForm.reason"
              placeholder="Nhập lý do cụ thể..."
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-medium text-slate-800 focus:outline-none focus:ring-2 focus:ring-rose-500/30"
            />
          </div>

          <!-- Modal Footer -->
          <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
            <button 
              type="button" 
              @click="showAddBusyModal = false"
              class="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 font-semibold text-slate-600"
            >
              Hủy
            </button>
            <button 
              type="submit"
              :disabled="isSavingBusy"
              class="px-5 py-2 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-bold transition shadow-sm"
            >
              <span v-if="isSavingBusy">Đang lưu...</span>
              <span v-else>Lưu Lịch Bận</span>
            </button>
          </div>

        </form>

      </div>
    </div>



  </div>
</template>
