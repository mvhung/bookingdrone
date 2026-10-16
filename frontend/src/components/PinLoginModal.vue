<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '../stores/auth';
import { useJobsStore } from '../stores/jobs';
import type { UserPickerDto } from '../types';
import { SERVICE_CATALOG } from '../types';

const props = defineProps<{
  closable?: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const authStore = useAuthStore();
const jobsStore = useJobsStore();

const selectedUser = ref<UserPickerDto | null>(null);
const pin = ref('');
const errorMsg = ref('');
const isSubmitting = ref(false);

onMounted(async () => {
  if (authStore.teamMembers.length === 0) {
    await authStore.fetchTeamMembers();
  }
});

function selectUser(user: UserPickerDto) {
  selectedUser.value = user;
  pin.value = '';
  errorMsg.value = '';
}

function appendPin(digit: string) {
  if (pin.value.length < 4) {
    pin.value += digit;
    if (pin.value.length === 4) {
      handleLogin();
    }
  }
}

function backspacePin() {
  pin.value = pin.value.slice(0, -1);
  errorMsg.value = '';
}

function clearPin() {
  pin.value = '';
  errorMsg.value = '';
}

async function handleLogin() {
  if (!selectedUser.value || pin.value.length !== 4) return;
  isSubmitting.value = true;
  errorMsg.value = '';

  const success = await authStore.login(selectedUser.value.id, pin.value);
  isSubmitting.value = false;

  if (success) {
    await jobsStore.loadJobs();
    emit('close');
  } else {
    errorMsg.value = authStore.error || 'Mã PIN không đúng (thử 1234)';
    pin.value = '';
  }
}

function getServiceIcon(code: string) {
  return SERVICE_CATALOG.find(s => s.code === code)?.icon || '✨';
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-md">
    <div 
      class="relative w-full max-w-lg bg-white/95 backdrop-blur-xl border border-slate-200/80 shadow-2xl rounded-3xl p-6 sm:p-8 overflow-hidden"
    >
      <!-- Close button if closable -->
      <button 
        v-if="closable" 
        @click="$emit('close')"
        class="absolute top-4 right-4 w-8 h-8 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 transition"
      >
        ✕
      </button>

      <!-- Step 1: Select User Avatar -->
      <div v-if="!selectedUser" class="space-y-6">
        <div class="text-center space-y-1">
          <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-indigo-50 border border-indigo-200 text-indigo-700 text-xs font-semibold mb-1">
            <span>🛡️</span> Hệ Thống Nội Bộ Team
          </div>
          <h2 class="text-xl sm:text-2xl font-extrabold text-slate-900 tracking-tight">
            EventMap & Crew Scheduler
          </h2>
          <p class="text-xs text-slate-500">
            Chọn khuôn mặt đại diện của bạn để đăng nhập
          </p>
        </div>

        <div class="grid grid-cols-4 gap-3 sm:gap-4 max-h-[380px] overflow-y-auto p-1">
          <button
            v-for="user in authStore.teamMembers"
            :key="user.id"
            @click="selectUser(user)"
            type="button"
            class="group flex flex-col items-center p-3 rounded-2xl border border-slate-200/80 hover:border-indigo-500 bg-white hover:bg-indigo-50/50 hover:shadow-lg transition-all transform hover:-translate-y-0.5"
          >
            <div class="relative w-14 h-14 rounded-2xl overflow-hidden bg-slate-100 border-2 border-white shadow-sm mb-2 group-hover:scale-105 transition">
              <img 
                :src="user.avatarUrl || `https://api.dicebear.com/7.x/bottts/svg?seed=${user.name}`" 
                :alt="user.name"
                class="w-full h-full object-cover" 
              />
            </div>
            <span class="text-xs font-bold text-slate-800 group-hover:text-indigo-600 truncate max-w-full">
              {{ user.name }}
            </span>
            <div class="flex items-center gap-0.5 mt-1">
              <span 
                v-for="sv in user.services.slice(0, 3)" 
                :key="sv" 
                class="text-[10px]" 
                :title="sv"
              >
                {{ getServiceIcon(sv) }}
              </span>
            </div>
          </button>
        </div>

        <div class="text-center text-[11px] text-slate-400">
          Chỉ các thành viên trong đội ngũ mới có quyền truy cập dữ liệu công việc được phân công.
        </div>
      </div>

      <!-- Step 2: Enter 4-digit PIN -->
      <div v-else class="space-y-6">
        <div class="flex items-center justify-between">
          <button 
            @click="selectedUser = null; pin = ''; errorMsg = ''"
            class="inline-flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-slate-800 transition"
          >
            ← Chọn người khác
          </button>
          <span class="text-[11px] text-indigo-600 bg-indigo-50 px-2 py-0.5 rounded-full font-medium">
            PIN mặc định: 1234
          </span>
        </div>

        <div class="flex flex-col items-center text-center space-y-2">
          <div class="w-16 h-16 rounded-2xl overflow-hidden bg-slate-100 border-2 border-indigo-200 shadow-md">
            <img 
              :src="selectedUser.avatarUrl || `https://api.dicebear.com/7.x/bottts/svg?seed=${selectedUser.name}`" 
              :alt="selectedUser.name"
              class="w-full h-full object-cover" 
            />
          </div>
          <div>
            <h3 class="text-lg font-bold text-slate-900">Xin chào, {{ selectedUser.name }}!</h3>
            <p class="text-xs text-slate-400">Nhập mã PIN 4 chữ số bảo mật</p>
          </div>
        </div>

        <!-- PIN Dots Display -->
        <div class="flex justify-center items-center gap-3 py-2">
          <div 
            v-for="i in 4" 
            :key="i"
            class="w-4 h-4 rounded-full border-2 transition-all duration-200"
            :class="[
              pin.length >= i 
                ? 'bg-indigo-600 border-indigo-600 scale-110 shadow-sm' 
                : 'bg-slate-100 border-slate-300'
            ]"
          ></div>
        </div>

        <!-- Error Message -->
        <div v-if="errorMsg" class="text-center text-xs text-rose-500 font-semibold bg-rose-50 border border-rose-200 py-1.5 px-3 rounded-xl animate-shake">
          {{ errorMsg }}
        </div>

        <!-- Keypad -->
        <div class="max-w-[260px] mx-auto grid grid-cols-3 gap-2.5">
          <button
            v-for="digit in ['1', '2', '3', '4', '5', '6', '7', '8', '9']"
            :key="digit"
            @click="appendPin(digit)"
            type="button"
            class="h-12 rounded-2xl bg-slate-50 hover:bg-slate-100 active:bg-indigo-100 border border-slate-200/90 text-slate-800 font-bold text-lg transition flex items-center justify-center shadow-xs"
          >
            {{ digit }}
          </button>
          <button
            @click="clearPin"
            type="button"
            class="h-12 rounded-2xl bg-slate-50 hover:bg-slate-100 active:bg-slate-200 border border-slate-200/90 text-slate-500 font-semibold text-xs transition flex items-center justify-center"
          >
            Xoá
          </button>
          <button
            @click="appendPin('0')"
            type="button"
            class="h-12 rounded-2xl bg-slate-50 hover:bg-slate-100 active:bg-indigo-100 border border-slate-200/90 text-slate-800 font-bold text-lg transition flex items-center justify-center shadow-xs"
          >
            0
          </button>
          <button
            @click="backspacePin"
            type="button"
            class="h-12 rounded-2xl bg-slate-50 hover:bg-slate-100 active:bg-slate-200 border border-slate-200/90 text-slate-600 font-bold text-sm transition flex items-center justify-center"
          >
            ⌫
          </button>
        </div>

        <!-- Quick 1234 button for ease of use -->
        <div class="text-center">
          <button
            @click="pin = '1234'; handleLogin()"
            type="button"
            class="text-[11px] font-semibold text-indigo-600 hover:text-indigo-800 hover:underline"
          >
            ⚡ Đăng nhập nhanh với PIN mẫu (1234)
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
