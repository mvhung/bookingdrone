<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAuthStore } from '../stores/auth';
import { useJobsStore } from '../stores/jobs';
import { useVenuesStore } from '../stores/venues';
import { availabilityApi } from '../services/api';
import type { AvailabilityItemDto, SaveJobRequest } from '../types';
import { SERVICE_CATALOG } from '../types';

const authStore = useAuthStore();
const jobsStore = useJobsStore();
const venuesStore = useVenuesStore();

const isSaving = ref(false);
const isDeleting = ref(false);
const availabilityList = ref<AvailabilityItemDto[]>([]);
const isCheckingAvailability = ref(false);

interface LocalCrewItem {
  userId: number;
  userName: string;
  castAmount: number;
  isPaid: boolean;
  personalNote: string;
}

const form = ref({
  id: null as number | null,
  eventDate: '2026-10-18',
  startTime: '08:00',
  endTime: '11:30',
  serviceCode: 'BUTTERFLY',
  locationName: '',
  subAddress: '',
  latitude: 21.003,
  longitude: 105.805,
  ownerUserId: null as number | null,
  clientTotalRevenue: null as number | null,
  externalWorkersNote: '',
  note: '',
  crew: [] as LocalCrewItem[],
  nearbyPois: [] as string[]
});

const currentUserId = computed(() => authStore.currentUser?.id ?? 0);

// PERMISSION CHECK: Only the Creator or the Owner can edit / delete
const canEdit = computed(() => {
  if (jobsStore.drawerMode === 'create') return true;
  const job = jobsStore.activeJob;
  if (!job) return false;
  return job.canEdit === true;
});

// Initialize form from store when drawer opens or activeJob/draftLocation changes
function initForm() {
  if (jobsStore.drawerMode === 'edit' && jobsStore.activeJob) {
    const job = jobsStore.activeJob;
    form.value = {
      id: job.id,
      eventDate: job.eventDate,
      startTime: job.startTime,
      endTime: job.endTime,
      serviceCode: job.serviceCode,
      locationName: job.locationName,
      subAddress: job.subAddress,
      latitude: job.latitude,
      longitude: job.longitude,
      ownerUserId: job.ownerUserId,
      clientTotalRevenue: job.clientTotalRevenue,
      externalWorkersNote: job.externalWorkersNote || '',
      note: job.note || '',
      nearbyPois: [],
      crew: job.assignments.map(a => ({
        userId: a.userId,
        userName: a.userName,
        castAmount: a.castAmount ?? 2000000,
        isPaid: a.isPaid,
        personalNote: a.personalNote || ''
      }))
    };
  } else if (jobsStore.drawerMode === 'create') {
    const loc = jobsStore.draftLocation;
    const defaultService = authStore.currentUser?.services[0] || 'BUTTERFLY';
    form.value = {
      id: null,
      eventDate: jobsStore.selectedDate || '2026-10-18',
      startTime: '08:00',
      endTime: '11:30',
      serviceCode: defaultService,
      locationName: loc?.location || 'Địa điểm mới',
      subAddress: loc?.subAddress || '',
      latitude: loc?.lat || 21.003,
      longitude: loc?.lng || 105.805,
      ownerUserId: currentUserId.value,
      clientTotalRevenue: null,
      externalWorkersNote: '',
      note: '',
      nearbyPois: loc?.nearbyPois || [],
      crew: [
        {
          userId: currentUserId.value,
          userName: authStore.currentUser?.name || 'Tôi',
          castAmount: 2000000,
          isPaid: false,
          personalNote: ''
        }
      ]
    };
  }

  fetchAvailability();
}

watch(
  () => [jobsStore.drawerOpen, jobsStore.activeJob, jobsStore.draftLocation],
  () => {
    if (jobsStore.drawerOpen) {
      initForm();
    }
  },
  { immediate: true }
);

// Reactively update form location & coordinates when reverse geocoding completes
watch(
  () => [jobsStore.draftLocation?.location, jobsStore.draftLocation?.lat, jobsStore.draftLocation?.lng],
  ([newLoc, newLat, newLng]) => {
    if (jobsStore.drawerMode === 'create' && newLoc) {
      if (
        !form.value.locationName || 
        form.value.locationName === 'Đang xác định...' || 
        form.value.locationName === 'Địa điểm mới' || 
        form.value.locationName === 'Vị trí đã chọn'
      ) {
        form.value.locationName = newLoc as string;
      }
      if (jobsStore.draftLocation?.subAddress && (!form.value.subAddress || form.value.subAddress === 'Hà Nội')) {
        form.value.subAddress = jobsStore.draftLocation.subAddress;
      }
      if (jobsStore.draftLocation?.nearbyPois) {
        form.value.nearbyPois = jobsStore.draftLocation.nearbyPois;
      }
      if (typeof newLat === 'number') {
        form.value.latitude = newLat;
      }
      if (typeof newLng === 'number') {
        form.value.longitude = newLng;
      }
    }
  }
);

function selectPoi(poi: string) {
  form.value.locationName = poi;
  const coords = jobsStore.draftLocation?.poiCoordinates?.[poi];
  if (coords) {
    form.value.latitude = coords.lat;
    form.value.longitude = coords.lng;
    if (jobsStore.draftLocation) {
      jobsStore.draftLocation.lat = coords.lat;
      jobsStore.draftLocation.lng = coords.lng;
    }
  }
}


// Fetch Blind Availability when Date, Time, Service, or Job changes
async function fetchAvailability() {
  if (!form.value.eventDate || !form.value.startTime || !form.value.endTime || !form.value.serviceCode) return;
  isCheckingAvailability.value = true;
  try {
    availabilityList.value = await availabilityApi.check(
      form.value.eventDate,
      form.value.startTime,
      form.value.endTime,
      form.value.serviceCode,
      form.value.id
    );
  } catch (err) {
    console.error('Failed to check availability', err);
  } finally {
    isCheckingAvailability.value = false;
  }
}

function add4Hours(timeStr: string): string {
  if (!timeStr) return '12:00';
  const parts = timeStr.split(':');
  let h = (parseInt(parts[0], 10) || 0) + 4;
  if (h >= 24) h = h - 24;
  const hh = h.toString().padStart(2, '0');
  const mm = parts[1] || '00';
  return `${hh}:${mm}`;
}

watch(
  () => form.value.startTime,
  (newStart) => {
    if (newStart) {
      form.value.endTime = add4Hours(newStart);
    }
  }
);

watch(
  () => [form.value.eventDate, form.value.startTime, form.value.endTime, form.value.serviceCode],
  () => {
    fetchAvailability();
  }
);

function isCrewSelected(userId: number): boolean {
  return form.value.crew.some(c => c.userId === userId);
}

function onAddCrewFromDropdown(event: Event) {
  const selectEl = event.target as HTMLSelectElement;
  const val = selectEl.value;
  if (!val) return;
  const userId = Number(val);
  const candidate = availabilityList.value.find(c => c.userId === userId);
  if (candidate && !isCrewSelected(userId)) {
    form.value.crew.push({
      userId: candidate.userId,
      userName: candidate.name,
      castAmount: 2000000,
      isPaid: false,
      personalNote: ''
    });
  }
  selectEl.value = '';
}

function removeCrew(userId: number) {
  if (!canEdit.value) return;
  const idx = form.value.crew.findIndex(c => c.userId === userId);
  if (idx >= 0) {
    form.value.crew.splice(idx, 1);
  }
}

// Financial calculations
const totalCrewPayout = computed(() => {
  return form.value.crew.reduce((sum, c) => sum + (c.castAmount || 0), 0);
});

const ownerProfit = computed(() => {
  if (form.value.clientTotalRevenue == null) return null;
  return form.value.clientTotalRevenue - totalCrewPayout.value;
});

// Crew items relevant for non-owner
const myCrewItems = computed(() => {
  return form.value.crew.filter(c => c.userId === currentUserId.value);
});

async function handleSave() {
  if (!canEdit.value) return;

  if (!form.value.locationName.trim()) {
    alert('Vui lòng nhập tên địa điểm!');
    return;
  }

  isSaving.value = true;
  const payload: SaveJobRequest = {
    eventDate: form.value.eventDate,
    startTime: form.value.startTime,
    endTime: form.value.endTime,
    serviceCode: form.value.serviceCode,
    locationName: form.value.locationName.trim(),
    subAddress: form.value.subAddress.trim(),
    latitude: form.value.latitude,
    longitude: form.value.longitude,
    ownerUserId: form.value.ownerUserId,
    clientTotalRevenue: form.value.clientTotalRevenue,
    externalWorkersNote: form.value.externalWorkersNote.trim(),
    note: form.value.note.trim(),
    assignments: form.value.crew.map(c => ({
      userId: c.userId,
      castAmount: c.castAmount,
      isPaid: c.isPaid,
      personalNote: c.personalNote
    }))
  };

  try {
    if (jobsStore.drawerMode === 'create') {
      await jobsStore.createJob(payload);
    } else if (form.value.id) {
      await jobsStore.updateJob(form.value.id, payload);
    }

    // Auto sync venue in frontend store
    if (payload.locationName && payload.locationName !== 'Địa điểm mới' && payload.locationName !== 'Vị trí đã chọn') {
      try {
        await venuesStore.saveVenue({
          name: payload.locationName,
          subAddress: payload.subAddress,
          latitude: payload.latitude,
          longitude: payload.longitude
        });
      } catch {}
    }
  } catch (err: any) {
    alert(err.message || 'Lưu thất bại!');
  } finally {
    isSaving.value = false;
  }
}

async function handleDelete() {
  if (!canEdit.value || !form.value.id) return;
  if (!confirm('Bạn có chắc chắn muốn xoá Job sự kiện này?')) return;

  isDeleting.value = true;
  try {
    await jobsStore.deleteJob(form.value.id);
  } catch (err: any) {
    alert(err.message || 'Xoá thất bại!');
  } finally {
    isDeleting.value = false;
  }
}

const servicesList = computed(() => authStore.services.length > 0 ? authStore.services : SERVICE_CATALOG);

function getMemberName(userId: number) {
  const member = authStore.teamMembers.find(m => m.id === userId);
  return member?.name || `Thợ #${userId}`;
}

function getServiceLabel(code: string) {
  const s = servicesList.value.find(x => x.code === code);
  return s ? `${s.icon} ${s.name}` : code;
}

function formatDateVN(dateStr: string) {
  if (!dateStr) return '';
  return dateStr.split('-').reverse().join('/');
}
</script>

<template>
  <aside 
    class="absolute top-0 right-0 h-full w-full sm:w-[400px] bg-white border-l border-slate-200/90 shadow-2xl z-30 flex flex-col select-none"
  >
    <!-- HEADER: Location Name IS the primary Job Title -->
    <div class="p-4 border-b border-slate-100 bg-slate-50/80 space-y-2 shrink-0">
      <div class="flex items-start justify-between gap-2">
        <div class="flex-1 min-w-0">
          
          <!-- Mode badge if Read-Only -->
          <div v-if="!canEdit" class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-slate-200/80 text-slate-700 font-semibold text-[10px] mb-1">
            <span>🔒</span> Chỉ xem (Chủ: {{ jobsStore.activeJob?.ownerUserName || jobsStore.activeJob?.createdByUserName }})
          </div>

          <!-- Editable location name if canEdit, else static text -->
          <div v-if="canEdit" class="flex items-center gap-2">
            <span class="text-rose-500 text-base shrink-0">📍</span>
            <input 
              v-model="form.locationName" 
              placeholder="Tên địa điểm..."
              class="w-full bg-transparent font-extrabold text-base text-slate-900 placeholder-slate-400 focus:bg-white focus:px-2.5 focus:py-1 rounded-xl focus:outline-none focus:ring-2 focus:ring-indigo-500/30 transition truncate"
            />
          </div>
          <div v-else class="flex items-center gap-2 font-extrabold text-base text-slate-900 truncate">
            <span class="text-rose-500 text-base shrink-0">📍</span>
            <span class="truncate">{{ form.locationName }}</span>
          </div>

          <!-- SubAddress -->
          <input 
            v-if="canEdit"
            v-model="form.subAddress" 
            placeholder="Địa chỉ chi tiết..."
            class="w-full bg-transparent text-xs text-slate-400 pl-6 focus:bg-white focus:px-2.5 focus:py-0.5 rounded-lg focus:outline-none transition truncate"
          />
          <div v-else class="text-xs text-slate-400 pl-6 truncate">
            {{ form.subAddress || 'Chưa có địa chỉ chi tiết' }}
          </div>
        </div>

        <button 
          @click="jobsStore.closeDrawer"
          type="button"
          class="w-8 h-8 rounded-full bg-white border border-slate-200 hover:bg-slate-100 flex items-center justify-center text-slate-500 text-xs shrink-0 transition shadow-xs"
        >
          ✕
        </button>
      </div>

      <!-- Quick POI Chips from Overpass/Photon (only in edit/create mode) -->
      <div v-if="canEdit && form.nearbyPois.length > 1" class="flex flex-wrap gap-1 pl-6 pt-0.5">
        <button 
          v-for="(poi, idx) in form.nearbyPois" 
          :key="idx" 
          type="button" 
          @click="selectPoi(poi)"
          class="text-[10px] bg-white hover:bg-indigo-600 hover:text-white text-slate-600 border border-slate-200 px-2 py-0.5 rounded-md transition shadow-2xs font-medium"
        >
          {{ poi }}
        </button>
      </div>
    </div>

    <!-- BODY: ULTRA-COMPACT FORM -->
    <div class="flex-1 min-h-0 overflow-y-auto p-4 space-y-3.5 text-xs">

      <!-- EDIT / CREATE MODE -->
      <template v-if="canEdit">
        <!-- ROW 1: NGÀY & GIỜ DỰ KIẾN (Tự động +4h giờ kết thúc) -->
        <div class="grid grid-cols-2 gap-2.5">
          <div>
            <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
              Ngày
            </label>
            <input 
              type="date" 
              v-model="form.eventDate"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5 font-bold text-slate-800 text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
            />
          </div>
          <div>
            <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
              Giờ dự kiến
            </label>
            <input 
              type="time" 
              v-model="form.startTime"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5 font-bold text-center text-slate-800 text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
            />
          </div>
        </div>

        <!-- ROW 2: DỊCH VỤ & CHỌN THỢ (CÙNG 1 HÀNG DROPDOWN) -->
        <div class="grid grid-cols-2 gap-2.5">
          <div>
            <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
              Dịch Vụ
            </label>
            <select 
              v-model="form.serviceCode"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5 font-bold text-slate-800 text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
            >
              <option v-for="s in servicesList" :key="s.code" :value="s.code">
                {{ s.icon }} {{ s.name }}
              </option>
            </select>
          </div>

          <div>
            <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
              Chọn Thợ
            </label>
            <select 
              @change="onAddCrewFromDropdown($event)"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5 font-bold text-slate-800 text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20 cursor-pointer"
            >
              <option value="">+ Chọn thợ...</option>
              <option 
                v-for="m in availabilityList" 
                :key="m.userId" 
                :value="m.userId"
                :disabled="isCrewSelected(m.userId)"
              >
                {{ m.isBusy ? '🔴 Bận' : '🟢 Rảnh' }}: {{ m.name }} {{ isCrewSelected(m.userId) ? '(Đã chọn)' : '' }}
              </option>
            </select>
          </div>
        </div>

        <!-- HÀNG CAST THỢ KHI ĐÃ CHỌN (KHÔNG CÓ TRẠNG THÁI THANH TOÁN) -->
        <div v-if="form.crew.length > 0 || form.externalWorkersNote" class="space-y-1.5 pt-1">
          <label class="block text-[11px] font-bold text-slate-400 uppercase">
            Thợ Chạy & Cát-xê ({{ form.crew.length }})
          </label>
          
          <div class="space-y-1.5">
            <div 
              v-for="c in form.crew" 
              :key="c.userId"
              class="flex items-center justify-between px-2.5 py-1.5 bg-slate-50 border border-slate-200/80 rounded-xl text-xs"
            >
              <div class="flex items-center gap-1.5 min-w-0">
                <button 
                  type="button" 
                  @click="removeCrew(c.userId)"
                  class="w-4 h-4 rounded-full bg-slate-200 hover:bg-rose-100 text-slate-500 hover:text-rose-600 flex items-center justify-center text-[10px] font-bold shrink-0 transition cursor-pointer"
                  title="Bỏ thợ này"
                >
                  ✕
                </button>
                <span class="font-bold text-slate-800 truncate">{{ c.userName || getMemberName(c.userId) }}</span>
              </div>

              <div class="flex items-center gap-1 shrink-0">
                <span class="text-[10px] text-slate-400 font-bold">Cast:</span>
                <input 
                  type="number" 
                  v-model.number="c.castAmount" 
                  step="100000"
                  class="bg-white border border-slate-200 rounded-lg px-2 py-0.5 font-bold text-emerald-700 w-24 text-right text-xs focus:outline-none"
                />
                <span class="text-[10px] font-bold text-slate-500">đ</span>
              </div>
            </div>

            <!-- Thêm thợ ngoài -->
            <div class="flex items-center gap-2 pt-0.5">
              <input 
                v-model="form.externalWorkersNote" 
                placeholder="+ Thêm thợ ngoài (nếu có)..."
                class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1 text-xs text-slate-700 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
              />
            </div>
          </div>
        </div>

        <!-- ROW 3: CHỦ JOB & TỔNG THU KHÁCH -->
        <div class="grid grid-cols-2 gap-2.5 pt-1">
          <div>
            <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
              Chủ Job
            </label>
            <select 
              v-model="form.ownerUserId"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5 font-bold text-slate-800 text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
            >
              <option :value="null">Khách ngoài</option>
              <option v-for="m in authStore.teamMembers" :key="m.id" :value="m.id">
                {{ m.name }}
              </option>
            </select>
          </div>
          <div>
            <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
              Tổng thu khách
            </label>
            <div class="relative">
              <input 
                type="number" 
                v-model.number="form.clientTotalRevenue" 
                step="100000"
                placeholder="0"
                class="w-full bg-slate-50 border border-slate-200 rounded-xl pl-2.5 pr-6 py-1.5 font-bold text-right text-slate-900 text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
              />
              <span class="absolute right-2.5 top-1.5 text-xs text-slate-400 font-bold">đ</span>
            </div>
          </div>
        </div>

        <!-- Lãi chênh lệch (nếu có thu khách) -->
        <div 
          v-if="form.clientTotalRevenue != null" 
          class="flex items-center justify-between px-3 py-1 bg-indigo-50/70 border border-indigo-200/60 rounded-xl text-xs font-bold"
        >
          <span class="text-indigo-900">Lãi chênh:</span>
          <span class="text-indigo-700 font-black">
            {{ (ownerProfit ?? 0).toLocaleString('vi-VN') }} đ
          </span>
        </div>

        <!-- GHI CHÚ DƯỚI CÙNG -->
        <div class="pt-1">
          <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
            Ghi Chú
          </label>
          <input 
            v-model="form.note" 
            placeholder="Nhập ghi chú cho tiệc này (nếu có)..."
            class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5 text-slate-800 text-xs placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
          />
        </div>
      </template>

      <!-- READ-ONLY SUMMARY (CHO THỢ ĐƯỢC GÁN) -->
      <template v-else>
        <div class="space-y-3 bg-slate-50 border border-slate-200 p-3.5 rounded-2xl font-medium">
          <div class="flex items-center justify-between font-bold text-slate-800">
            <span>📅 {{ formatDateVN(form.eventDate) }}</span>
            <span class="text-indigo-600 font-extrabold text-xs">⏰ {{ form.startTime }} - {{ form.endTime }}</span>
          </div>

          <div class="flex items-center justify-between pt-1 border-t border-slate-200/70">
            <span class="text-slate-400 font-bold text-[11px] uppercase">Dịch vụ:</span>
            <span class="px-2.5 py-0.5 rounded-lg bg-indigo-100 text-indigo-800 font-bold text-xs">
              {{ getServiceLabel(form.serviceCode) }}
            </span>
          </div>

          <div class="flex items-center justify-between pt-1 border-t border-slate-200/70">
            <span class="text-slate-400 font-bold text-[11px] uppercase">Chủ Job:</span>
            <span class="font-bold text-slate-800">
              {{ jobsStore.activeJob?.ownerUserName || jobsStore.activeJob?.createdByUserName || 'Khách lẻ' }}
            </span>
          </div>

          <div class="pt-1 border-t border-slate-200/70 space-y-1">
            <span class="text-slate-400 font-bold text-[11px] uppercase block">Thợ cùng chạy:</span>
            <div class="flex flex-wrap gap-1">
              <span 
                v-for="c in form.crew" 
                :key="c.userId"
                class="px-2 py-0.5 rounded-lg bg-white border border-slate-200 text-slate-700 font-bold text-[11px]"
              >
                {{ c.userName || getMemberName(c.userId) }}
              </span>
              <span v-if="form.externalWorkersNote" class="px-2 py-0.5 rounded-lg bg-amber-50 border border-amber-200 text-amber-800 font-semibold text-[11px]">
                + {{ form.externalWorkersNote }}
              </span>
            </div>
          </div>

          <div v-if="myCrewItems.length > 0" class="flex items-center justify-between pt-2 border-t border-slate-200/70 bg-white p-2.5 rounded-xl border">
            <span class="font-bold text-slate-700">Cát-xê của bạn:</span>
            <span class="font-black text-emerald-700 text-sm">
              {{ (myCrewItems[0].castAmount ?? 0).toLocaleString('vi-VN') }} đ
            </span>
          </div>

          <div v-if="form.note" class="pt-1 border-t border-slate-200/70 text-slate-600 text-xs">
            <span class="text-slate-400 font-bold text-[10px] uppercase block mb-0.5">Ghi chú:</span>
            {{ form.note }}
          </div>
        </div>
      </template>

    </div>

    <!-- FOOTER: Chỉ chủ/người tạo mới có nút Lưu và Xoá -->
    <div class="p-3.5 border-t border-slate-100 bg-white flex items-center justify-between shrink-0 shadow-lg">
      <template v-if="canEdit">
        <button 
          v-if="jobsStore.drawerMode === 'edit'" 
          @click="handleDelete" 
          :disabled="isDeleting"
          type="button"
          class="px-3 py-1.5 rounded-xl bg-rose-50 hover:bg-rose-100 text-rose-600 text-xs font-bold transition flex items-center gap-1 cursor-pointer"
        >
          <span>🗑️</span>
          <span>{{ isDeleting ? 'Đang xoá...' : 'Xoá' }}</span>
        </button>
        <div v-else></div>

        <div class="flex items-center gap-2">
          <button 
            @click="jobsStore.closeDrawer" 
            type="button"
            class="px-3.5 py-1.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-600 text-xs font-semibold transition cursor-pointer"
          >
            Đóng
          </button>
          <button 
            @click="handleSave" 
            :disabled="isSaving"
            type="button"
            class="px-5 py-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md hover:shadow-indigo-500/25 transition flex items-center gap-1.5 cursor-pointer"
          >
            <span v-if="isSaving">Đang lưu...</span>
            <span v-else>Lưu Job</span>
          </button>
        </div>
      </template>

      <!-- If cannot edit (Assigned crew): only Close button -->
      <template v-else>
        <div></div>
        <button 
          @click="jobsStore.closeDrawer" 
          type="button"
          class="px-6 py-2 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold transition shadow-xs cursor-pointer"
        >
          Đóng
        </button>
      </template>
    </div>
  </aside>
</template>
