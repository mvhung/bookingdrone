<script setup lang="ts">
import { ref, reactive } from 'vue';
import { useAuthStore } from '../stores/auth';
import { SERVICE_CATALOG } from '../types';

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const authStore = useAuthStore();
const user = authStore.currentUser;

const form = reactive({
  services: [...(user?.services || [])],
  officeBusyEnabled: user?.officeBusyEnabled || false,
  officeBusyStart: user?.officeBusyStart || '08:00',
  officeBusyEnd: user?.officeBusyEnd || '17:30',
});

const newBusyDate = ref('2026-10-25');
const newBusyReason = ref('Việc riêng');
const isSaving = ref(false);
const saveSuccess = ref(false);

function toggleService(code: string) {
  const idx = form.services.indexOf(code);
  if (idx >= 0) {
    form.services.splice(idx, 1);
  } else {
    form.services.push(code);
  }
}

async function handleAddBusyDate() {
  if (!newBusyDate.value) return;
  await authStore.addBusyDate(newBusyDate.value, newBusyReason.value);
}

async function handleRemoveBusyDate(id: number) {
  await authStore.removeBusyDate(id);
}

async function handleSave() {
  isSaving.value = true;
  saveSuccess.value = false;
  const success = await authStore.updateProfile({
    services: form.services,
    officeBusyEnabled: form.officeBusyEnabled,
    officeBusyStart: form.officeBusyStart,
    officeBusyEnd: form.officeBusyEnd,
  });
  isSaving.value = false;
  if (success) {
    saveSuccess.value = true;
    setTimeout(() => {
      saveSuccess.value = false;
      emit('close');
    }, 800);
  }
}

function formatDateVN(dateStr: string) {
  if (!dateStr) return '';
  return dateStr.split('-').reverse().join('/');
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm">
    <div 
      class="relative w-full max-w-md bg-white border border-slate-200 shadow-2xl rounded-3xl p-5 sm:p-6 space-y-4 text-xs max-h-[90vh] overflow-y-auto"
    >
      <!-- Header -->
      <div class="flex items-center justify-between border-b border-slate-100 pb-3">
        <div class="flex items-center gap-2.5">
          <div class="w-10 h-10 rounded-xl overflow-hidden bg-slate-100 border border-slate-200">
            <img 
              :src="user?.avatarUrl || `https://api.dicebear.com/7.x/bottts/svg?seed=${user?.name}`" 
              :alt="user?.name"
              class="w-full h-full object-cover" 
            />
          </div>
          <div>
            <h3 class="font-bold text-sm text-slate-900">⚙️ Cài đặt của {{ user?.name }}</h3>
            <p class="text-[11px] text-slate-400">Dịch vụ cung cấp & lịch bận hành chính</p>
          </div>
        </div>
        <button 
          @click="$emit('close')"
          class="w-7 h-7 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 transition"
        >
          ✕
        </button>
      </div>

      <!-- 1. Dịch vụ cung cấp -->
      <div class="space-y-2">
        <label class="block font-bold text-slate-700 uppercase text-[11px]">
          1. Kỹ Năng / Dịch Vụ Bạn Nhận Chạy
        </label>
        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="sv in SERVICE_CATALOG"
            :key="sv.code"
            type="button"
            @click="toggleService(sv.code)"
            class="px-3 py-1.5 rounded-xl border font-semibold flex items-center gap-1.5 transition"
            :class="[
              form.services.includes(sv.code)
                ? 'bg-indigo-600 text-white border-indigo-600 shadow-xs'
                : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'
            ]"
          >
            <span>{{ sv.icon }}</span>
            <span>{{ sv.name }}</span>
            <span v-if="form.services.includes(sv.code)">✓</span>
          </button>
        </div>
      </div>

      <!-- 2. Lịch bận cố định T2 - T6 (Hành chính) -->
      <div class="space-y-2.5 pt-3 border-t border-slate-100">
        <label class="block font-bold text-slate-700 uppercase text-[11px]">
          2. Lịch Bận Cố Định (Đi Làm Giờ Hành Chính)
        </label>
        
        <label class="flex items-center gap-2.5 cursor-pointer bg-slate-50 p-3 rounded-2xl border border-slate-200/90 hover:bg-slate-100/60 transition">
          <input 
            type="checkbox" 
            v-model="form.officeBusyEnabled"
            class="w-4 h-4 rounded text-indigo-600 focus:ring-indigo-500 cursor-pointer"
          />
          <div class="flex-1">
            <span class="font-bold text-slate-800 text-xs">Thứ 2 ➔ Thứ 6 bận giờ hành chính</span>
            <p class="text-[10px] text-slate-400">Hệ thống sẽ báo bận khi có ai chọn bạn vào khung giờ này</p>
          </div>
        </label>

        <div v-if="form.officeBusyEnabled" class="flex items-center gap-2 pl-3 py-1 bg-amber-50/70 border border-amber-200/70 rounded-xl px-3">
          <span class="text-amber-800 font-semibold text-[11px]">Khung giờ:</span>
          <input 
            type="time" 
            v-model="form.officeBusyStart"
            class="bg-white border border-amber-300 rounded-lg px-2 py-1 font-bold text-slate-800 text-center focus:outline-none"
          />
          <span class="text-amber-600 font-bold">đến</span>
          <input 
            type="time" 
            v-model="form.officeBusyEnd"
            class="bg-white border border-amber-300 rounded-lg px-2 py-1 font-bold text-slate-800 text-center focus:outline-none"
          />
        </div>
      </div>

      <!-- 3. Ngày bận đột xuất (Nghỉ riêng) -->
      <div class="space-y-2.5 pt-3 border-t border-slate-100">
        <label class="block font-bold text-slate-700 uppercase text-[11px]">
          3. Báo Nghỉ Ngày Cụ Thể (Đi Du Lịch, Việc Riêng)
        </label>
        
        <div class="flex items-center gap-2">
          <input 
            type="date" 
            v-model="newBusyDate"
            class="bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5 font-semibold text-slate-800 focus:outline-none"
          />
          <input 
            type="text" 
            v-model="newBusyReason"
            placeholder="Lý do (tuỳ chọn)..."
            class="flex-1 bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5 text-slate-700 focus:outline-none"
          />
          <button 
            type="button"
            @click="handleAddBusyDate"
            class="bg-slate-900 hover:bg-slate-800 text-white font-semibold px-3 py-1.5 rounded-xl transition shrink-0"
          >
            + Thêm
          </button>
        </div>

        <!-- Busy dates list -->
        <div class="flex flex-wrap gap-1.5 pt-1 min-h-[32px]">
          <span 
            v-for="b in user?.customBusyDates" 
            :key="b.id"
            class="inline-flex items-center gap-1.5 bg-rose-50 text-rose-700 border border-rose-200 px-2.5 py-1 rounded-xl font-medium"
          >
            <span>📅 {{ formatDateVN(b.date) }}</span>
            <span v-if="b.reason" class="text-[10px] text-rose-500">({{ b.reason }})</span>
            <button 
              type="button"
              @click="handleRemoveBusyDate(b.id)"
              class="text-rose-400 hover:text-rose-700 font-bold ml-0.5"
            >
              ✕
            </button>
          </span>
          <span v-if="!user?.customBusyDates || user.customBusyDates.length === 0" class="text-slate-400 italic text-[11px]">
            Chưa có ngày nghỉ đột xuất nào
          </span>
        </div>
      </div>

      <!-- Footer Buttons -->
      <div class="flex items-center justify-between pt-3 border-t border-slate-100">
        <span v-if="saveSuccess" class="text-emerald-600 font-bold text-xs flex items-center gap-1">
          ✓ Đã lưu thay đổi!
        </span>
        <div v-else></div>

        <div class="flex items-center gap-2">
          <button 
            type="button"
            @click="$emit('close')"
            class="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-600 font-semibold transition"
          >
            Đóng
          </button>
          <button 
            type="button"
            @click="handleSave"
            :disabled="isSaving"
            class="px-5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-semibold transition shadow-sm flex items-center gap-1.5"
          >
            <span v-if="isSaving">Đang lưu...</span>
            <span v-else>Lưu Thiết Lập</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
