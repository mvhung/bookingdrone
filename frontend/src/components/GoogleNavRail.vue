<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useAuthStore } from '../stores/auth';
import { authApi } from '../services/api';

const props = defineProps<{
  activeTab?: 'map' | 'calendar' | 'admin' | 'profile';
}>();

const emit = defineEmits<{
  (e: 'select-tab', tab: 'map' | 'calendar' | 'admin' | 'profile'): void;
  (e: 'export-excel'): void;
  (e: 'open-login'): void;
}>();

const authStore = useAuthStore();
const isAdmin = computed(() => authStore.isAdmin);

// User Menu Popover State
const showUserPopover = ref(false);
const popoverRef = ref<HTMLElement | null>(null);

function toggleUserPopover() {
  showUserPopover.value = !showUserPopover.value;
}

function closeUserPopover(e: MouseEvent) {
  if (popoverRef.value && !popoverRef.value.contains(e.target as Node)) {
    showUserPopover.value = false;
  }
}

onMounted(() => {
  window.addEventListener('click', closeUserPopover);
});

onUnmounted(() => {
  window.removeEventListener('click', closeUserPopover);
});

// Logout handler
function handleLogout() {
  showUserPopover.value = false;
  if (confirm('Bạn có chắc muốn đăng xuất khỏi hệ thống?')) {
    authStore.logout();
    emit('open-login');
  }
}

// Change PIN Modal State
const showChangePinModal = ref(false);
const oldPin = ref('');
const newPin = ref('');
const isChangingPin = ref(false);

function openChangePin() {
  showUserPopover.value = false;
  oldPin.value = '';
  newPin.value = '';
  showChangePinModal.value = true;
}

async function submitChangePin() {
  if (!newPin.value || newPin.value.length < 4) {
    alert('Mã PIN mới phải có ít nhất 4 chữ số');
    return;
  }
  isChangingPin.value = true;
  try {
    const res = await authApi.changePin({
      oldPin: oldPin.value,
      newPin: newPin.value
    });
    alert(res.message || 'Đổi mật khẩu thành công!');
    showChangePinModal.value = false;
  } catch (err: any) {
    alert(err.message || 'Lỗi khi đổi mật khẩu');
  } finally {
    isChangingPin.value = false;
  }
}
</script>

<template>
  <aside 
    class="absolute top-0 bottom-0 left-0 z-30 w-16 sm:w-[68px] bg-white border-r border-slate-200/90 shadow-md flex flex-col items-center py-3 select-none transition-all"
    aria-label="Google Maps Sidebar"
  >
    <!-- TOP: Google Maps Menu Icon / Logo -->
    <div class="mb-4">
      <button 
        type="button" 
        @click="$emit('select-tab', 'map')"
        class="w-11 h-11 rounded-2xl flex items-center justify-center text-slate-700 hover:bg-slate-100 active:scale-95 transition cursor-pointer"
        title="Bản đồ sự kiện (Trang chính)"
      >
        <svg class="w-6 h-6 text-slate-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
        </svg>
      </button>
    </div>

    <!-- MAIN NAV ITEMS -->
    <nav class="flex-1 flex flex-col items-center gap-2.5 w-full px-1.5">
      
      <!-- 1. Map View Item -->
      <button 
        type="button" 
        @click="$emit('select-tab', 'map')"
        class="group flex flex-col items-center justify-center w-full py-2.5 px-1 rounded-2xl transition relative cursor-pointer"
        :class="activeTab === 'map' ? 'bg-indigo-50 text-indigo-700' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'"
      >
        <div 
          class="w-9 h-9 rounded-xl flex items-center justify-center text-xl transition group-hover:scale-105"
          :class="activeTab === 'map' ? 'bg-indigo-600 text-white shadow-xs' : 'text-slate-600'"
        >
          🗺️
        </div>
        <span class="text-[10px] font-bold mt-1 text-center tracking-tight leading-tight">
          Bản đồ
        </span>
        <div 
          v-if="activeTab === 'map'"
          class="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-5 bg-indigo-600 rounded-r-full"
        ></div>
      </button>

      <!-- 2. Calendar Item -->
      <button 
        type="button" 
        @click="$emit('select-tab', 'calendar')"
        class="group flex flex-col items-center justify-center w-full py-2.5 px-1 rounded-2xl transition relative cursor-pointer"
        :class="activeTab === 'calendar' ? 'bg-indigo-50 text-indigo-700' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'"
      >
        <div 
          class="w-9 h-9 rounded-xl flex items-center justify-center text-xl transition group-hover:scale-105"
          :class="activeTab === 'calendar' ? 'bg-indigo-600 text-white shadow-xs' : 'text-slate-600'"
        >
          📅
        </div>
        <span class="text-[10px] font-bold mt-1 text-center tracking-tight leading-tight">
          Lịch biểu
        </span>
        <div 
          v-if="activeTab === 'calendar'"
          class="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-5 bg-indigo-600 rounded-r-full"
        ></div>
      </button>

      <!-- 3. Users & Services (Admin / Đội ngũ) -->
      <button 
        v-if="isAdmin"
        type="button" 
        @click="$emit('select-tab', 'admin')"
        class="group flex flex-col items-center justify-center w-full py-2.5 px-1 rounded-2xl transition relative cursor-pointer"
        :class="activeTab === 'admin' ? 'bg-indigo-50 text-indigo-700' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'"
      >
        <div 
          class="w-9 h-9 rounded-xl flex items-center justify-center text-xl transition group-hover:scale-105"
          :class="activeTab === 'admin' ? 'bg-indigo-600 text-white shadow-xs' : 'text-slate-600'"
        >
          ⚙️
        </div>
        <span class="text-[10px] font-bold mt-1 text-center tracking-tight leading-tight">
          Quản trị
        </span>
        <div 
          v-if="activeTab === 'admin'"
          class="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-5 bg-indigo-600 rounded-r-full"
        ></div>
      </button>

      <!-- 4. Quick Excel Export -->
      <button 
        type="button" 
        @click="$emit('export-excel')"
        class="group flex flex-col items-center justify-center w-full py-2.5 px-1 rounded-2xl transition text-slate-600 hover:bg-emerald-50 hover:text-emerald-700 cursor-pointer"
        title="Xuất bảng kê Excel theo tháng"
      >
        <div class="w-9 h-9 rounded-xl flex items-center justify-center text-xl transition group-hover:scale-105">
          📊
        </div>
        <span class="text-[10px] font-bold mt-1 text-center tracking-tight leading-tight">
          Báo cáo
        </span>
      </button>

    </nav>

    <!-- BOTTOM ITEM: USER AVATAR & ACCOUNT POPOVER (CHUYỂN XUỐNG DƯỚI NÀY) -->
    <div ref="popoverRef" class="relative w-full px-1.5 flex flex-col items-center pt-2 border-t border-slate-100">
      
      <!-- User Avatar Circle Button -->
      <button 
        type="button" 
        @click.stop="toggleUserPopover"
        class="w-10 h-10 rounded-full bg-linear-to-tr from-blue-600 to-indigo-600 text-white font-extrabold text-sm flex items-center justify-center shadow-md hover:shadow-lg ring-2 ring-white hover:ring-blue-300 active:scale-95 transition cursor-pointer overflow-hidden"
        :title="`Tài khoản: ${authStore.currentUser?.name} (Bấm để xem tài khoản)`"
      >
        <img 
          v-if="authStore.currentUser?.avatarUrl" 
          :src="authStore.currentUser.avatarUrl" 
          :alt="authStore.currentUser.name" 
          class="w-full h-full object-cover" 
        />
        <span v-else>
          {{ authStore.currentUser?.name ? authStore.currentUser.name.charAt(0).toUpperCase() : 'U' }}
        </span>
      </button>

      <span class="text-[9px] font-bold text-center text-slate-700 truncate max-w-full mt-1">
        {{ authStore.currentUser?.name }}
      </span>

      <!-- User Popover (Anchored at the bottom of the sidebar) -->
      <div 
        v-if="showUserPopover"
        class="absolute bottom-1 left-16 sm:left-[72px] z-50 w-64 bg-white rounded-3xl shadow-2xl border border-slate-200/90 p-3.5 select-none animate-in fade-in slide-in-from-left-2"
        @click.stop
      >
        <!-- Header: User Name & Role -->
        <div class="flex items-center gap-2.5 pb-3 border-b border-slate-100">
          <div class="w-10 h-10 rounded-full bg-linear-to-tr from-blue-600 to-indigo-600 text-white font-black text-sm flex items-center justify-center overflow-hidden shrink-0">
            <img 
              v-if="authStore.currentUser?.avatarUrl" 
              :src="authStore.currentUser.avatarUrl" 
              :alt="authStore.currentUser.name" 
              class="w-full h-full object-cover" 
            />
            <span v-else>
              {{ authStore.currentUser?.name ? authStore.currentUser.name.charAt(0).toUpperCase() : 'U' }}
            </span>
          </div>
          <div class="min-w-0 flex-1">
            <div class="font-extrabold text-slate-900 text-xs truncate">
              {{ authStore.currentUser?.name }}
            </div>
            <span 
              v-if="authStore.isAdmin" 
              class="inline-block mt-0.5 bg-amber-100 text-amber-800 text-[9px] font-bold px-2 py-0.2 rounded-md"
            >
              Quản trị viên
            </span>
            <span 
              v-else 
              class="inline-block mt-0.5 bg-slate-100 text-slate-600 text-[9px] font-semibold px-2 py-0.2 rounded-md"
            >
              Thành viên
            </span>
          </div>
        </div>

        <!-- 2 Actions Only: Đổi mật khẩu & Đăng xuất (Gọn gàng như yêu cầu) -->
        <div class="py-2 space-y-1">
          <button 
            type="button" 
            @click="openChangePin"
            class="w-full flex items-center gap-2 px-3 py-2 rounded-xl text-xs font-semibold text-slate-700 hover:bg-slate-100 transition text-left cursor-pointer"
          >
            <span>🔑</span>
            <span>Đổi mật khẩu (Mã PIN)</span>
          </button>
        </div>

        <div class="pt-2 border-t border-slate-100">
          <button 
            type="button" 
            @click="handleLogout"
            class="w-full flex items-center justify-center gap-2 px-3 py-2 rounded-xl text-xs font-bold text-rose-600 hover:bg-rose-50 transition cursor-pointer"
          >
            <span>🚪</span>
            <span>Đăng xuất</span>
          </button>
        </div>

      </div>

    </div>

    <!-- CHANGE PIN MODAL -->
    <div 
      v-if="showChangePinModal"
      class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 animate-in fade-in"
      @click.self="showChangePinModal = false"
    >
      <div class="bg-white rounded-3xl shadow-2xl border border-slate-200 w-full max-w-xs p-5 space-y-4">
        <div class="flex items-center justify-between border-b border-slate-100 pb-2">
          <h3 class="font-extrabold text-slate-900 text-sm">
            Đổi Mật Khẩu (Mã PIN)
          </h3>
          <button type="button" @click="showChangePinModal = false" class="text-slate-400 hover:text-slate-700 text-xs font-bold">✕</button>
        </div>

        <div class="space-y-3 text-xs">
          <div>
            <label class="block font-bold text-slate-700 mb-1">Mã PIN hiện tại:</label>
            <input 
              v-model="oldPin" 
              type="password"
              maxlength="6"
              placeholder="1234"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-center text-sm font-mono font-bold text-slate-800 focus:outline-none focus:border-indigo-500" 
            />
          </div>

          <div>
            <label class="block font-bold text-slate-700 mb-1">Mã PIN 4 số mới:</label>
            <input 
              v-model="newPin" 
              type="password"
              maxlength="6"
              placeholder="Nhập 4 số mới"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-center text-base font-mono font-bold tracking-widest text-slate-800 focus:outline-none focus:border-indigo-500" 
            />
          </div>
        </div>

        <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
          <button type="button" @click="showChangePinModal = false" class="px-3 py-1.5 rounded-xl text-slate-600 hover:bg-slate-100 font-bold text-xs cursor-pointer">
            Hủy
          </button>
          <button 
            type="button" 
            @click="submitChangePin" 
            :disabled="isChangingPin"
            class="px-4 py-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs shadow-xs transition cursor-pointer disabled:opacity-50"
          >
            {{ isChangingPin ? 'Đang lưu...' : 'Lưu mật khẩu' }}
          </button>
        </div>
      </div>
    </div>

  </aside>
</template>
