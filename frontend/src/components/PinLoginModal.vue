<script setup lang="ts">
import { ref } from 'vue';
import { useAuthStore } from '../stores/auth';
import { useJobsStore } from '../stores/jobs';

const props = defineProps<{
  closable?: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const authStore = useAuthStore();
const jobsStore = useJobsStore();

const userName = ref('');
const pin = ref('');
const errorMsg = ref('');
const isSubmitting = ref(false);

function appendPin(digit: string) {
  if (pin.value.length < 6) {
    pin.value += digit;
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
  if (!userName.value.trim()) {
    errorMsg.value = 'Vui lòng nhập tên của bạn';
    return;
  }
  if (!pin.value || pin.value.length < 4) {
    errorMsg.value = 'Vui lòng nhập mã PIN (ít nhất 4 số)';
    return;
  }

  isSubmitting.value = true;
  errorMsg.value = '';

  const success = await authStore.login(userName.value.trim(), pin.value);
  isSubmitting.value = false;

  if (success) {
    await jobsStore.loadJobs();
    emit('close');
  } else {
    errorMsg.value = authStore.error || 'Tên hoặc mã PIN không đúng';
  }
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-md">
    <div 
      class="relative w-full max-w-sm bg-white/95 backdrop-blur-xl border border-slate-200/90 shadow-2xl rounded-3xl p-6 sm:p-7 overflow-hidden text-xs"
    >
      <!-- Close button if closable -->
      <button 
        v-if="closable" 
        @click="$emit('close')"
        type="button"
        class="absolute top-4 right-4 w-8 h-8 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 transition font-bold"
      >
        ✕
      </button>

      <!-- Header -->
      <div class="text-center space-y-1 mb-5">
        <div class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-indigo-50 border border-indigo-200 text-indigo-700 text-[11px] font-bold mb-1">
          <span>🛡️</span> Hệ Thống Nội Bộ Team
        </div>
        <h2 class="text-xl font-extrabold text-slate-900 tracking-tight">
          Đăng Nhập EventMap
        </h2>
        <p class="text-[11px] text-slate-500">
          Nhập tên thành viên và mã PIN cá nhân
        </p>
      </div>

      <!-- Form -->
      <form @submit.prevent="handleLogin" class="space-y-4">
        <!-- Username input -->
        <div>
          <label class="block font-bold text-slate-700 mb-1">Tên Thành Viên</label>
          <div class="relative">
            <span class="absolute left-3 top-2.5 text-slate-400 text-sm">👤</span>
            <input 
              type="text" 
              v-model="userName" 
              required
              autofocus
              placeholder="VD: Hưng, Minh, Tuấn..."
              class="w-full bg-slate-50 border border-slate-200 rounded-xl pl-9 pr-3 py-2.5 font-bold text-slate-900 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/30 transition text-sm"
            />
          </div>
        </div>

        <!-- PIN Input -->
        <div>
          <div class="flex items-center justify-between mb-1">
            <label class="block font-bold text-slate-700">Mã PIN</label>
            <span class="text-[10px] text-slate-400">Mặc định: 1234</span>
          </div>
          <div class="relative">
            <span class="absolute left-3 top-2.5 text-slate-400 text-sm">🔑</span>
            <input 
              type="password" 
              v-model="pin" 
              required
              maxlength="6"
              placeholder="••••"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl pl-9 pr-3 py-2.5 font-extrabold text-slate-900 tracking-widest text-base focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/30 transition"
            />
          </div>
        </div>

        <!-- Numeric Keypad for fast touch/click -->
        <div class="grid grid-cols-3 gap-2 pt-1 max-w-[240px] mx-auto">
          <button
            v-for="d in ['1', '2', '3', '4', '5', '6', '7', '8', '9']"
            :key="d"
            type="button"
            @click="appendPin(d)"
            class="h-10 rounded-xl bg-slate-50 hover:bg-indigo-50 hover:text-indigo-600 border border-slate-200/90 font-bold text-sm text-slate-700 transition active:scale-95 shadow-2xs"
          >
            {{ d }}
          </button>
          <button
            type="button"
            @click="clearPin"
            class="h-10 rounded-xl bg-slate-50 hover:bg-slate-100 border border-slate-200/90 text-slate-500 font-semibold text-xs transition"
          >
            Xóa
          </button>
          <button
            type="button"
            @click="appendPin('0')"
            class="h-10 rounded-xl bg-slate-50 hover:bg-indigo-50 hover:text-indigo-600 border border-slate-200/90 font-bold text-sm text-slate-700 transition active:scale-95 shadow-2xs"
          >
            0
          </button>
          <button
            type="button"
            @click="backspacePin"
            class="h-10 rounded-xl bg-slate-50 hover:bg-slate-100 border border-slate-200/90 text-slate-500 font-semibold text-xs transition"
          >
            ⌫
          </button>
        </div>

        <!-- Error display -->
        <div v-if="errorMsg" class="p-2.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 font-semibold text-center text-[11px] animate-shake">
          {{ errorMsg }}
        </div>

        <!-- Submit Button -->
        <button
          type="submit"
          :disabled="isSubmitting"
          class="w-full py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-sm shadow-md hover:shadow-indigo-500/25 transition active:scale-98 flex items-center justify-center gap-1.5"
        >
          <span v-if="isSubmitting">Đang kiểm tra...</span>
          <span v-else>Đăng Nhập ➔</span>
        </button>
      </form>

    </div>
  </div>
</template>
