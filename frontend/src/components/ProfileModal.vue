<script setup lang="ts">
import { ref, reactive, computed } from 'vue';
import { useAuthStore } from '../stores/auth';
import { SERVICE_CATALOG } from '../types';

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const authStore = useAuthStore();
const user = authStore.currentUser;

const servicesList = computed(() => authStore.services.length > 0 ? authStore.services : SERVICE_CATALOG);

const form = reactive({
  services: [...(user?.services || [])],
});

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

async function handleSave() {
  isSaving.value = true;
  saveSuccess.value = false;
  const success = await authStore.updateProfile({
    services: form.services,
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
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm">
    <div 
      class="relative w-full max-w-md bg-white border border-slate-200 shadow-2xl rounded-3xl flex flex-col max-h-[88vh] overflow-hidden text-xs"
    >
      <!-- Header -->
      <div class="px-5 py-4 border-b border-slate-100 flex items-center justify-between shrink-0 bg-slate-50/70">
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
            <p class="text-[11px] text-slate-400">Dịch vụ & vai trò bạn nhận chạy job</p>
          </div>
        </div>
        <button 
          @click="$emit('close')"
          class="w-7 h-7 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 transition cursor-pointer"
        >
          ✕
        </button>
      </div>

      <!-- Scrollable Body -->
      <div class="flex-1 min-h-0 overflow-y-auto p-5 space-y-4 pb-6">
        <!-- 1. Dịch vụ cung cấp -->
        <div class="space-y-2">
          <label class="block font-bold text-slate-700 uppercase text-[11px]">
            Kỹ Năng / Dịch Vụ Bạn Nhận Chạy
          </label>
          <div class="flex flex-wrap gap-1.5">
            <button
              v-for="sv in servicesList"
              :key="sv.code"
              type="button"
              @click="toggleService(sv.code)"
              class="px-3 py-1.5 rounded-xl border font-semibold flex items-center gap-1.5 transition cursor-pointer"
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
      </div>

      <!-- Sticky Footer Buttons -->
      <div class="px-5 py-3.5 border-t border-slate-100 bg-white flex items-center justify-between shrink-0 shadow-lg">
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
