<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAuthStore } from '../stores/auth';
import { useJobsStore } from '../stores/jobs';
import { availabilityApi } from '../services/api';
import type { AvailabilityItemDto, SaveJobRequest } from '../types';
import { SERVICE_CATALOG } from '../types';

const authStore = useAuthStore();
const jobsStore = useJobsStore();

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

watch(
  () => [form.value.eventDate, form.value.startTime, form.value.endTime, form.value.serviceCode],
  () => {
    fetchAvailability();
  }
);

function changeService(code: string) {
  form.value.serviceCode = code;
}

function isCrewSelected(userId: number): boolean {
  return form.value.crew.some(c => c.userId === userId);
}

function toggleCrew(candidate: AvailabilityItemDto) {
  if (!canEdit.value) return;
  const idx = form.value.crew.findIndex(c => c.userId === candidate.userId);
  if (idx >= 0) {
    form.value.crew.splice(idx, 1);
  } else {
    form.value.crew.push({
      userId: candidate.userId,
      userName: candidate.name,
      castAmount: 2000000,
      isPaid: false,
      personalNote: ''
    });
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

function getMemberName(userId: number) {
  const member = authStore.teamMembers.find(m => m.id === userId);
  return member?.name || `Thợ #${userId}`;
}

function getServiceLabel(code: string) {
  const s = SERVICE_CATALOG.find(x => x.code === code);
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
    <div class="p-4 border-b border-slate-100 bg-slate-50/80 space-y-2">
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
          @click="form.locationName = poi"
          class="text-[10px] bg-white hover:bg-indigo-600 hover:text-white text-slate-600 border border-slate-200 px-2 py-0.5 rounded-md transition shadow-2xs font-medium"
        >
          {{ poi }}
        </button>
      </div>
    </div>

    <!-- BODY: 5 STEPS -->
    <div class="flex-1 overflow-y-auto p-4 space-y-4 text-xs">

      <!-- STEP 1: NGÀY & GIỜ CHẠY -->
      <div v-if="canEdit" class="grid grid-cols-12 gap-2">
        <div class="col-span-5">
          <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
            Ngày
          </label>
          <input 
            type="date" 
            v-model="form.eventDate"
            class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-2 font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
          />
        </div>
        <div class="col-span-7">
          <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
            Khung Giờ
          </label>
          <div class="flex items-center gap-1.5">
            <input 
              type="time" 
              v-model="form.startTime"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2 py-2 font-bold text-center text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
            />
            <span class="text-slate-300 font-bold">-</span>
            <input 
              type="time" 
              v-model="form.endTime"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2 py-2 font-bold text-center text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
            />
          </div>
        </div>
      </div>
      <!-- Step 1 Read-only -->
      <div v-else class="bg-slate-50 border border-slate-200 px-3.5 py-2 rounded-xl flex items-center justify-between font-bold text-slate-800">
        <span>📅 {{ formatDateVN(form.eventDate) }}</span>
        <span class="text-indigo-600 font-extrabold text-xs">⏰ {{ form.startTime }} - {{ form.endTime }}</span>
      </div>

      <!-- STEP 2: DỊCH VỤ -->
      <div v-if="canEdit">
        <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1.5">
          Dịch Vụ
        </label>
        <div class="flex flex-wrap gap-1.5">
          <button 
            v-for="sv in SERVICE_CATALOG" 
            :key="sv.code" 
            type="button"
            @click="changeService(sv.code)"
            class="px-3 py-1.5 rounded-xl border font-semibold flex items-center gap-1.5 transition"
            :class="[
              form.serviceCode === sv.code 
                ? 'bg-indigo-600 text-white border-indigo-600 shadow-xs' 
                : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
            ]"
          >
            <span>{{ sv.icon }}</span>
            <span>{{ sv.name }}</span>
          </button>
        </div>
      </div>
      <!-- Step 2 Read-only -->
      <div v-else class="flex items-center gap-2">
        <span class="text-[11px] font-bold text-slate-400 uppercase">Dịch vụ:</span>
        <span class="px-3 py-1 rounded-xl bg-indigo-50 border border-indigo-200 text-indigo-700 font-bold text-xs">
          {{ getServiceLabel(form.serviceCode) }}
        </span>
      </div>

      <!-- STEP 3: THỢ CHẠY (Chỉ hiện chấm đỏ/xanh, không chú thích dài dòng) -->
      <div>
        <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1.5">
          Thợ Chạy
        </label>

        <!-- Candidate Chips for Edit Mode -->
        <div v-if="canEdit" class="space-y-2">
          <div class="flex flex-wrap gap-1.5">
            <button 
              v-for="candidate in availabilityList" 
              :key="candidate.userId" 
              type="button" 
              @click="toggleCrew(candidate)"
              class="px-3 py-1.5 rounded-xl border font-semibold flex items-center gap-1.5 transition"
              :class="[
                isCrewSelected(candidate.userId) 
                  ? 'bg-slate-900 text-white border-slate-900 shadow-xs' 
                  : 'bg-white border-slate-200 text-slate-700 hover:border-slate-300'
              ]" 
            >
              <!-- Chấm xanh / đỏ gọn gàng -->
              <span 
                class="w-2 h-2 rounded-full shrink-0"
                :class="candidate.isBusy ? 'bg-rose-500' : 'bg-emerald-500'"
              ></span>
              <span>{{ candidate.name }}</span>
              <span v-if="isCrewSelected(candidate.userId)" class="text-indigo-300 text-xs font-bold">
                ✓
              </span>
            </button>
          </div>

          <!-- Thêm thợ ngoài: icon + và chữ Thêm -->
          <div class="flex items-center gap-1.5 pt-1">
            <span class="text-slate-400 text-sm font-bold pl-1">+</span>
            <input 
              v-model="form.externalWorkersNote" 
              placeholder="Thêm thợ ngoài..."
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-1.5 text-slate-700 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
            />
          </div>
        </div>

        <!-- Read-only Crew Display -->
        <div v-else class="space-y-1.5">
          <div class="flex flex-wrap gap-1.5">
            <span 
              v-for="c in form.crew" 
              :key="c.userId"
              class="px-3 py-1 rounded-xl bg-slate-100 border border-slate-200 font-bold text-slate-800 flex items-center gap-1.5"
            >
              <span>👤</span>
              <span>{{ c.userName || getMemberName(c.userId) }}</span>
            </span>
            <span 
              v-if="form.externalWorkersNote"
              class="px-3 py-1 rounded-xl bg-amber-50 border border-amber-200 font-semibold text-amber-800 flex items-center gap-1"
            >
              <span>+</span>
              <span>{{ form.externalWorkersNote }}</span>
            </span>
          </div>
        </div>
      </div>

      <!-- STEP 4: CHỦ JOB & GHI CHÚ -->
      <div v-if="canEdit" class="grid grid-cols-2 gap-2">
        <div>
          <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
            Chủ Job
          </label>
          <select 
            v-model="form.ownerUserId"
            class="w-full bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-2 font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
          >
            <option :value="null">Khách ngoài</option>
            <option v-for="m in authStore.teamMembers" :key="m.id" :value="m.id">
              {{ m.name }}
            </option>
          </select>
        </div>
        <div>
          <label class="block text-[11px] font-bold text-slate-400 uppercase mb-1">
            Ghi Chú
          </label>
          <input 
            v-model="form.note" 
            placeholder="Ghi chú..."
            class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
          />
        </div>
      </div>
      <!-- Step 4 Read-only -->
      <div v-else class="space-y-1 bg-slate-50 border border-slate-200 p-3 rounded-xl">
        <div class="flex items-center justify-between">
          <span class="text-slate-400 font-bold uppercase text-[10px]">Chủ Job:</span>
          <span class="font-bold text-slate-800">
            {{ jobsStore.activeJob?.ownerUserName || 'Khách lẻ' }}
          </span>
        </div>
        <div v-if="form.note" class="pt-1 border-t border-slate-200/60 text-slate-600">
          <span class="text-slate-400 font-bold uppercase text-[10px] block">Ghi chú:</span>
          {{ form.note }}
        </div>
      </div>

      <!-- STEP 5: CÁT-XÊ & THANH TOÁN (Tự động hiển thị theo vai trò, không nút ẩn) -->
      <div class="bg-slate-50 border border-slate-200/90 rounded-2xl p-3.5 space-y-3">
        <div class="flex items-center gap-1.5">
          <span class="text-xs">💰</span>
          <span class="text-[11px] font-extrabold text-slate-700 uppercase tracking-tight">
            Cát-xê & Thanh toán
          </span>
        </div>

        <!-- 1. NẾU LÀ CHỦ JOB / NGƯỜI TẠO (CAN EDIT): Xem được hết -->
        <template v-if="canEdit">
          <!-- Tổng thu khách -->
          <div class="flex items-center justify-between bg-white px-3 py-2 rounded-xl border border-slate-200 shadow-2xs">
            <span class="font-bold text-slate-700 text-[11px]">Tổng thu khách:</span>
            <div class="flex items-center gap-1">
              <input 
                type="number" 
                v-model.number="form.clientTotalRevenue" 
                step="100000"
                placeholder="Chưa nhập..."
                class="bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1 font-extrabold text-slate-900 w-32 text-right focus:outline-none focus:bg-white focus:ring-1 focus:ring-indigo-500"
              />
              <span class="font-bold text-slate-600 text-[11px]">đ</span>
            </div>
          </div>

          <!-- Chi trả từng thợ -->
          <div class="space-y-1.5">
            <div 
              v-for="c in form.crew" 
              :key="c.userId"
              class="flex items-center justify-between bg-white px-3 py-1.5 rounded-xl border border-slate-200 shadow-2xs"
            >
              <span class="font-bold text-slate-700">
                Trả {{ c.userName || getMemberName(c.userId) }}:
              </span>
              <div class="flex items-center gap-1.5">
                <input 
                  type="number" 
                  v-model.number="c.castAmount" 
                  step="100000"
                  class="bg-slate-50 border border-slate-200 rounded-lg px-2 py-1 font-bold text-emerald-700 w-24 text-right focus:outline-none"
                />
                <button 
                  type="button" 
                  @click="c.isPaid = !c.isPaid"
                  class="px-2 py-1 rounded-lg font-bold text-[10px] transition"
                  :class="c.isPaid ? 'bg-emerald-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
                >
                  {{ c.isPaid ? '✓ Đã TT' : 'Chờ TT' }}
                </button>
              </div>
            </div>
          </div>

          <!-- Lợi nhuận chênh lệch -->
          <div 
            v-if="form.clientTotalRevenue != null" 
            class="flex items-center justify-between px-3 py-1.5 bg-indigo-50/70 border border-indigo-200/70 rounded-xl font-bold text-[11px]"
          >
            <span class="text-indigo-900">Lãi chênh:</span>
            <span class="text-indigo-700 font-extrabold text-xs">
              {{ (ownerProfit ?? 0).toLocaleString('vi-VN') }} đ
            </span>
          </div>
        </template>

        <!-- 2. NẾU LÀ THỢ ĐƯỢC PHÂN CÔNG (READ ONLY): Chỉ xem cát-xê của chính mình -->
        <template v-else>
          <div 
            v-if="myCrewItems.length > 0"
            class="flex items-center justify-between bg-white px-3.5 py-2 rounded-xl border border-slate-200 shadow-2xs"
          >
            <span class="font-bold text-slate-700">Cát-xê của bạn:</span>
            <div class="flex items-center gap-2">
              <span class="font-black text-emerald-700 text-sm">
                {{ (myCrewItems[0].castAmount ?? 0).toLocaleString('vi-VN') }} đ
              </span>
              <span 
                class="px-2 py-0.5 rounded-lg font-bold text-[10px]"
                :class="myCrewItems[0].isPaid ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'"
              >
                {{ myCrewItems[0].isPaid ? '✓ Đã TT' : 'Chờ TT' }}
              </span>
            </div>
          </div>

          <div v-else class="text-slate-400 text-center py-1">
            Không có thông tin cát-xê
          </div>
        </template>

      </div>

    </div>

    <!-- FOOTER: Chỉ chủ/người tạo mới có nút Lưu và Xoá -->
    <div class="p-4 border-t border-slate-100 bg-white flex items-center justify-between">
      <template v-if="canEdit">
        <button 
          v-if="jobsStore.drawerMode === 'edit'" 
          @click="handleDelete" 
          :disabled="isDeleting"
          type="button"
          class="px-3.5 py-2 rounded-xl bg-rose-50 hover:bg-rose-100 text-rose-600 text-xs font-bold transition flex items-center gap-1"
        >
          <span>🗑️</span>
          <span>{{ isDeleting ? 'Đang xoá...' : 'Xoá' }}</span>
        </button>
        <div v-else></div>

        <div class="flex items-center gap-2">
          <button 
            @click="jobsStore.closeDrawer" 
            type="button"
            class="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-600 text-xs font-semibold transition"
          >
            Đóng
          </button>
          <button 
            @click="handleSave" 
            :disabled="isSaving"
            type="button"
            class="px-5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md hover:shadow-indigo-500/25 transition flex items-center gap-1.5"
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
          class="px-6 py-2 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold transition shadow-xs"
        >
          Đóng
        </button>
      </template>
    </div>
  </aside>
</template>
