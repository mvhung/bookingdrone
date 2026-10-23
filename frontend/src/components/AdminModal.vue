<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { adminApi, servicesApi } from '../services/api';
import { useAuthStore } from '../stores/auth';
import type { AdminUserDto, ServiceTypeDto, CreateUserRequest, UpdateUserAdminRequest, SaveServiceTypeRequest } from '../types';

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const authStore = useAuthStore();

// Current active tab: 'users' | 'services'
const activeTab = ref<'users' | 'services'>('users');

const loading = ref(false);
const users = ref<AdminUserDto[]>([]);
const services = ref<ServiceTypeDto[]>([]);

// Predefined avatar seeds for quick picker
const avatarSeeds = ['Hung', 'Minh', 'Tuan', 'Hai', 'Long', 'Khanh', 'Viet', 'Tung', 'Felix', 'Jack', 'Oliver', 'Leo'];

onMounted(async () => {
  await loadAllData();
});

async function loadAllData() {
  loading.value = true;
  try {
    const [uList, sList] = await Promise.all([
      adminApi.getUsers(),
      servicesApi.getServices(true)
    ]);
    users.value = uList;
    services.value = sList;
    authStore.services = sList.filter(s => s.isActive);
  } catch (err: any) {
    console.error('Failed to load admin data', err);
  } finally {
    loading.value = false;
  }
}

// ================= USER MANAGEMENT =================
const showUserModal = ref(false);
const isEditingUser = ref(false);
const editingUserId = ref<number | null>(null);

const userForm = ref<{
  name: string;
  pin: string;
  avatarUrl: string;
  services: string[];
  isAdmin: boolean;
  officeBusyEnabled: boolean;
  officeBusyStart: string;
  officeBusyEnd: string;
}>({
  name: '',
  pin: '1234',
  avatarUrl: '',
  services: [],
  isAdmin: false,
  officeBusyEnabled: false,
  officeBusyStart: '08:00',
  officeBusyEnd: '17:30',
});

function openCreateUser() {
  isEditingUser.value = false;
  editingUserId.value = null;
  const randomSeed = avatarSeeds[Math.floor(Math.random() * avatarSeeds.length)];
  userForm.value = {
    name: '',
    pin: '1234',
    avatarUrl: `https://api.dicebear.com/7.x/bottts/svg?seed=${randomSeed}`,
    services: services.value.length > 0 ? [services.value[0].code] : [],
    isAdmin: false,
    officeBusyEnabled: false,
    officeBusyStart: '08:00',
    officeBusyEnd: '17:30',
  };
  showUserModal.value = true;
}

function openEditUser(u: AdminUserDto) {
  isEditingUser.value = true;
  editingUserId.value = u.id;
  userForm.value = {
    name: u.name,
    pin: '', // Not changed here, use Reset PIN instead
    avatarUrl: u.avatarUrl || '',
    services: [...u.services],
    isAdmin: u.isAdmin,
    officeBusyEnabled: u.officeBusyEnabled,
    officeBusyStart: u.officeBusyStart || '08:00',
    officeBusyEnd: u.officeBusyEnd || '17:30',
  };
  showUserModal.value = true;
}

function toggleUserService(code: string) {
  const idx = userForm.value.services.indexOf(code);
  if (idx >= 0) {
    userForm.value.services.splice(idx, 1);
  } else {
    userForm.value.services.push(code);
  }
}

const isSavingUser = ref(false);

async function saveUser() {
  if (!userForm.value.name.trim()) {
    alert('Vui lòng nhập tên thành viên');
    return;
  }

  isSavingUser.value = true;
  try {
    if (isEditingUser.value && editingUserId.value) {
      const payload: UpdateUserAdminRequest = {
        name: userForm.value.name.trim(),
        avatarUrl: userForm.value.avatarUrl,
        services: userForm.value.services,
        isAdmin: userForm.value.isAdmin,
        officeBusyEnabled: userForm.value.officeBusyEnabled,
        officeBusyStart: userForm.value.officeBusyStart,
        officeBusyEnd: userForm.value.officeBusyEnd,
      };
      await adminApi.updateUser(editingUserId.value, payload);
    } else {
      const payload: CreateUserRequest = {
        name: userForm.value.name.trim(),
        pin: userForm.value.pin || '1234',
        avatarUrl: userForm.value.avatarUrl,
        services: userForm.value.services,
        isAdmin: userForm.value.isAdmin,
      };
      await adminApi.createUser(payload);
    }
    showUserModal.value = false;
    await loadAllData();
    await authStore.fetchTeamMembers();
  } catch (err: any) {
    alert(err.message || 'Lỗi khi lưu thông tin thành viên');
  } finally {
    isSavingUser.value = false;
  }
}

// Reset PIN modal
const showResetPinModal = ref(false);
const resetPinUserId = ref<number | null>(null);
const resetPinUserName = ref('');
const newPinValue = ref('1234');
const isResettingPin = ref(false);

function openResetPin(u: AdminUserDto) {
  resetPinUserId.value = u.id;
  resetPinUserName.value = u.name;
  newPinValue.value = '1234';
  showResetPinModal.value = true;
}

async function submitResetPin() {
  if (!resetPinUserId.value || newPinValue.value.length < 4) {
    alert('Mã PIN mới phải có ít nhất 4 ký tự');
    return;
  }
  isResettingPin.value = true;
  try {
    const res = await adminApi.resetPin(resetPinUserId.value, newPinValue.value);
    alert(res.message);
    showResetPinModal.value = false;
  } catch (err: any) {
    alert(err.message || 'Lỗi khi đặt lại PIN');
  } finally {
    isResettingPin.value = false;
  }
}

async function deleteUser(u: AdminUserDto) {
  if (!confirm(`Bạn có chắc muốn xóa thành viên '${u.name}'?`)) return;
  try {
    await adminApi.deleteUser(u.id);
    await loadAllData();
    await authStore.fetchTeamMembers();
  } catch (err: any) {
    alert(err.message || 'Lỗi khi xóa thành viên');
  }
}

// ================= SERVICE TYPE MANAGEMENT =================
const showServiceModal = ref(false);
const isEditingService = ref(false);
const editingServiceId = ref<number | null>(null);

const serviceForm = ref<SaveServiceTypeRequest>({
  code: '',
  name: '',
  icon: '🎯',
  description: '',
  isActive: true,
  displayOrder: 0,
});

const quickIcons = ['🦋', '🚁', '✈️', '🎥', '📸', '✨', '📡', '🎬', '🎯', '💡', '🎤', '🎪'];

function openCreateService() {
  isEditingService.value = false;
  editingServiceId.value = null;
  serviceForm.value = {
    code: '',
    name: '',
    icon: '✨',
    description: '',
    isActive: true,
    displayOrder: services.value.length + 1,
  };
  showServiceModal.value = true;
}

function openEditService(s: ServiceTypeDto) {
  isEditingService.value = true;
  editingServiceId.value = s.id;
  serviceForm.value = {
    code: s.code,
    name: s.name,
    icon: s.icon,
    description: s.description || '',
    isActive: s.isActive,
    displayOrder: s.displayOrder,
  };
  showServiceModal.value = true;
}

const isSavingService = ref(false);

async function saveService() {
  if (!serviceForm.value.code.trim() || !serviceForm.value.name.trim()) {
    alert('Vui lòng nhập mã và tên dịch vụ');
    return;
  }

  isSavingService.value = true;
  try {
    if (isEditingService.value && editingServiceId.value) {
      await servicesApi.updateService(editingServiceId.value, serviceForm.value);
    } else {
      await servicesApi.createService(serviceForm.value);
    }
    showServiceModal.value = false;
    await loadAllData();
    await authStore.fetchServices();
  } catch (err: any) {
    alert(err.message || 'Lỗi khi lưu loại dịch vụ');
  } finally {
    isSavingService.value = false;
  }
}

async function deleteService(s: ServiceTypeDto) {
  if (!confirm(`Bạn có chắc muốn xóa hoặc ngưng dịch vụ '${s.name}'?`)) return;
  try {
    const res = await servicesApi.deleteService(s.id);
    if (res && res.message) {
      alert(res.message);
    }
    await loadAllData();
    await authStore.fetchServices();
  } catch (err: any) {
    alert(err.message || 'Lỗi khi xóa dịch vụ');
  }
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-5 bg-slate-950/70 backdrop-blur-md">
    <div class="relative w-full max-w-4xl bg-white border border-slate-200 shadow-2xl rounded-3xl flex flex-col max-h-[92vh] overflow-hidden text-xs">
      
      <!-- MODAL HEADER -->
      <div class="px-6 py-4 border-b border-slate-200 bg-slate-50/80 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-3">
          <span class="text-2xl">🛡️</span>
          <div>
            <h2 class="text-base font-extrabold text-slate-900 tracking-tight">
              Trung Tâm Quản Trị Hệ Thống (Admin Panel)
            </h2>
            <p class="text-[11px] text-slate-500">
              Quản lý danh sách thành viên đội ngũ & danh mục loại dịch vụ
            </p>
          </div>
        </div>

        <!-- Tab Switcher -->
        <div class="flex items-center bg-slate-200/80 p-1 rounded-2xl border border-slate-300/60">
          <button 
            @click="activeTab = 'users'"
            class="px-4 py-1.5 rounded-xl font-bold transition flex items-center gap-1.5"
            :class="activeTab === 'users' ? 'bg-white text-indigo-700 shadow-xs' : 'text-slate-600 hover:text-slate-900'"
          >
            <span>👥</span>
            <span>Thành Viên ({{ users.length }})</span>
          </button>
          <button 
            @click="activeTab = 'services'"
            class="px-4 py-1.5 rounded-xl font-bold transition flex items-center gap-1.5"
            :class="activeTab === 'services' ? 'bg-white text-indigo-700 shadow-xs' : 'text-slate-600 hover:text-slate-900'"
          >
            <span>🏷️</span>
            <span>Loại Dịch Vụ ({{ services.length }})</span>
          </button>
        </div>

        <button 
          @click="$emit('close')"
          class="w-8 h-8 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 font-bold transition"
        >
          ✕
        </button>
      </div>

      <!-- TAB 1: USERS MANAGEMENT -->
      <div v-if="activeTab === 'users'" class="flex-1 min-h-0 p-5 pb-28 overflow-y-auto space-y-4 bg-slate-50/50">
        <!-- Action Row -->
        <div class="flex items-center justify-between">
          <div class="text-xs text-slate-600 font-medium">
            Danh sách thợ trong đội ngũ và phân quyền
          </div>
          <button 
            @click="openCreateUser"
            class="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold flex items-center gap-1.5 shadow-xs transition"
          >
            <span>➕</span>
            <span>Thêm Thành Viên Mới</span>
          </button>
        </div>

        <!-- Users Table / Cards -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
          <div 
            v-for="u in users" 
            :key="u.id"
            class="bg-white border border-slate-200 rounded-2xl p-4 shadow-xs flex flex-col justify-between hover:border-indigo-300 transition"
          >
            <div class="flex items-start gap-3">
              <!-- Avatar -->
              <div class="w-12 h-12 rounded-2xl overflow-hidden bg-slate-100 border border-slate-200 shrink-0">
                <img :src="u.avatarUrl || `https://api.dicebear.com/7.x/bottts/svg?seed=${u.name}`" :alt="u.name" class="w-full h-full object-cover" />
              </div>

              <!-- Info -->
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2">
                  <h4 class="font-extrabold text-sm text-slate-900 truncate">{{ u.name }}</h4>
                  <span v-if="u.isAdmin" class="px-2 py-0.5 rounded-full bg-amber-100 text-amber-800 font-bold text-[10px] border border-amber-200 shrink-0">
                    👑 Admin
                  </span>
                </div>

                <div class="text-[11px] text-slate-500 mt-0.5">
                  <span v-if="u.officeBusyEnabled" class="text-amber-700 font-medium">
                    🏢 Bận HC ({{ u.officeBusyStart }}-{{ u.officeBusyEnd }})
                  </span>
                  <span v-else class="text-emerald-600 font-medium">
                    ⚡ Rảnh tự do
                  </span>
                </div>

                <!-- Services Tags -->
                <div class="flex flex-wrap gap-1 mt-2">
                  <span 
                    v-for="sCode in u.services" 
                    :key="sCode"
                    class="px-2 py-0.5 rounded-lg bg-indigo-50 text-indigo-700 border border-indigo-100 font-bold text-[10px]"
                  >
                    {{ sCode }}
                  </span>
                  <span v-if="u.services.length === 0" class="text-slate-400 italic text-[10px]">
                    Chưa gán dịch vụ
                  </span>
                </div>
              </div>
            </div>

            <!-- Action Buttons Footer -->
            <div class="flex items-center justify-end gap-1.5 pt-3 mt-3 border-t border-slate-100">
              <button 
                @click="openResetPin(u)"
                class="px-2.5 py-1 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 font-semibold text-[11px] transition flex items-center gap-1"
                title="Đặt lại mã PIN đăng nhập"
              >
                <span>🔑</span>
                <span>Đổi PIN</span>
              </button>
              <button 
                @click="openEditUser(u)"
                class="px-2.5 py-1 rounded-xl bg-indigo-50 hover:bg-indigo-100 text-indigo-700 font-semibold text-[11px] transition flex items-center gap-1"
              >
                <span>✏️</span>
                <span>Sửa</span>
              </button>
              <button 
                v-if="u.id !== authStore.currentUser?.id"
                @click="deleteUser(u)"
                class="px-2.5 py-1 rounded-xl bg-rose-50 hover:bg-rose-100 text-rose-700 font-semibold text-[11px] transition flex items-center gap-1"
              >
                <span>🗑️</span>
                <span>Xóa</span>
              </button>
            </div>

          </div>
        </div>
      </div>

      <!-- TAB 2: SERVICES MANAGEMENT -->
      <div v-if="activeTab === 'services'" class="flex-1 min-h-0 p-5 pb-28 overflow-y-auto space-y-4 bg-slate-50/50">
        <!-- Action Row -->
        <div class="flex items-center justify-between">
          <div class="text-xs text-slate-600 font-medium">
            Danh mục các dịch vụ sự kiện (Bướm, FPV, Flycam, Quay, Chụp, v.v.)
          </div>
          <button 
            @click="openCreateService"
            class="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold flex items-center gap-1.5 shadow-xs transition"
          >
            <span>➕</span>
            <span>Thêm Loại Dịch Vụ Mới</span>
          </button>
        </div>

        <!-- Services Grid -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          <div 
            v-for="s in services" 
            :key="s.id"
            class="bg-white border rounded-2xl p-4 shadow-xs flex flex-col justify-between transition"
            :class="s.isActive ? 'border-slate-200 hover:border-indigo-300' : 'border-slate-200 bg-slate-50/60 opacity-60'"
          >
            <div>
              <div class="flex items-center justify-between mb-2">
                <span class="text-2xl bg-slate-100 w-10 h-10 rounded-xl flex items-center justify-center">{{ s.icon }}</span>
                <span 
                  class="px-2 py-0.5 rounded-full font-bold text-[10px]"
                  :class="s.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-600'"
                >
                  {{ s.isActive ? 'Đang hoạt động' : 'Tạm ngưng' }}
                </span>
              </div>
              <h4 class="font-extrabold text-sm text-slate-900">{{ s.name }}</h4>
              <p class="text-[11px] font-mono text-indigo-600 font-bold">{{ s.code }}</p>
              <p v-if="s.description" class="text-[11px] text-slate-500 mt-1 line-clamp-2">
                {{ s.description }}
              </p>
            </div>

            <!-- Action Buttons Footer -->
            <div class="flex items-center justify-end gap-1.5 pt-3 mt-3 border-t border-slate-100">
              <button 
                @click="openEditService(s)"
                class="px-2.5 py-1 rounded-xl bg-indigo-50 hover:bg-indigo-100 text-indigo-700 font-semibold text-[11px] transition flex items-center gap-1"
              >
                <span>✏️</span>
                <span>Sửa</span>
              </button>
              <button 
                @click="deleteService(s)"
                class="px-2.5 py-1 rounded-xl bg-rose-50 hover:bg-rose-100 text-rose-700 font-semibold text-[11px] transition flex items-center gap-1"
              >
                <span>🗑️</span>
                <span>{{ s.isActive ? 'Xóa/Ngưng' : 'Xóa' }}</span>
              </button>
            </div>

          </div>
        </div>
      </div>

    </div>

    <!-- CREATE / EDIT USER MODAL -->
    <div 
      v-if="showUserModal" 
      class="fixed inset-0 z-60 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm"
      @click.self="showUserModal = false"
    >
      <div class="relative w-full max-w-md bg-white border border-slate-200 shadow-2xl rounded-3xl flex flex-col max-h-[85vh] overflow-hidden">
        <div class="p-5 border-b border-slate-100 flex items-center justify-between shrink-0 bg-slate-50/70">
          <h3 class="font-extrabold text-sm text-slate-900">
            {{ isEditingUser ? '✏️ Cập Nhật Thành Viên' : '➕ Thêm Thành Viên Mới' }}
          </h3>
          <button @click="showUserModal = false" class="w-7 h-7 rounded-full bg-slate-100 text-slate-500 font-bold">✕</button>
        </div>

        <form @submit.prevent="saveUser" class="flex-1 min-h-0 flex flex-col overflow-hidden">
          <div class="flex-1 min-h-0 overflow-y-auto p-5 space-y-3.5 text-xs pb-6">
          <!-- Name -->
          <div>
            <label class="block font-bold text-slate-700 mb-1">Tên thành viên (*)</label>
            <input 
              type="text" 
              v-model="userForm.name" 
              required
              placeholder="VD: Hùng, Hoàng..."
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-bold text-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500/30"
            />
          </div>

          <!-- PIN (Only on create) -->
          <div v-if="!isEditingUser">
            <label class="block font-bold text-slate-700 mb-1">Mã PIN đăng nhập (4 số)</label>
            <input 
              type="text" 
              v-model="userForm.pin" 
              maxlength="6"
              placeholder="1234"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-bold text-slate-800 text-center tracking-widest focus:outline-none focus:ring-2 focus:ring-indigo-500/30"
            />
          </div>

          <!-- Avatar URL & Presets -->
          <div>
            <label class="block font-bold text-slate-700 mb-1">Avatar đại diện</label>
            <div class="flex items-center gap-2 mb-2">
              <div class="w-10 h-10 rounded-xl overflow-hidden bg-slate-100 border border-slate-200 shrink-0">
                <img :src="userForm.avatarUrl" class="w-full h-full object-cover" />
              </div>
              <input 
                type="text" 
                v-model="userForm.avatarUrl" 
                placeholder="URL hình ảnh hoặc chọn bên dưới..."
                class="flex-1 bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-slate-700 focus:outline-none"
              />
            </div>
            <!-- Quick Seeds -->
            <div class="flex items-center gap-1.5 overflow-x-auto py-1">
              <button 
                v-for="s in avatarSeeds" 
                :key="s" 
                type="button"
                @click="userForm.avatarUrl = `https://api.dicebear.com/7.x/bottts/svg?seed=${s}`"
                class="w-7 h-7 rounded-lg overflow-hidden border shrink-0 hover:scale-110 transition"
              >
                <img :src="`https://api.dicebear.com/7.x/bottts/svg?seed=${s}`" class="w-full h-full" />
              </button>
            </div>
          </div>

          <!-- Services Multi Checkbox -->
          <div>
            <label class="block font-bold text-slate-700 mb-1.5">Kỹ năng / Dịch vụ nhận chạy</label>
            <div class="flex flex-wrap gap-1.5">
              <button 
                v-for="sv in services" 
                :key="sv.code"
                type="button"
                @click="toggleUserService(sv.code)"
                class="px-2.5 py-1 rounded-xl border font-semibold flex items-center gap-1 transition"
                :class="userForm.services.includes(sv.code) ? 'bg-indigo-600 text-white border-indigo-600' : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'"
              >
                <span>{{ sv.icon }}</span>
                <span>{{ sv.name }}</span>
                <span v-if="userForm.services.includes(sv.code)">✓</span>
              </button>
            </div>
          </div>

          <!-- Admin Flag Toggle -->
          <label class="flex items-center gap-2.5 cursor-pointer bg-amber-50/70 p-3 rounded-2xl border border-amber-200/90">
            <input type="checkbox" v-model="userForm.isAdmin" class="w-4 h-4 text-amber-600 rounded cursor-pointer" />
            <div>
              <span class="font-extrabold text-amber-900 text-xs">Quyền Quản Trị Viên (Admin)</span>
              <p class="text-[10px] text-amber-700">Được phép vào trang Admin để quản lý thành viên & dịch vụ</p>
            </div>
          </label>

          <!-- Office Hours Settings (If Editing) -->
          <div v-if="isEditingUser" class="space-y-2 pt-2 border-t border-slate-100">
            <label class="flex items-center gap-2 cursor-pointer bg-slate-50 p-2.5 rounded-xl border border-slate-200">
              <input type="checkbox" v-model="userForm.officeBusyEnabled" class="w-4 h-4 text-indigo-600 rounded cursor-pointer" />
              <span class="font-bold text-slate-800">Thứ 2 - Thứ 6 bận giờ hành chính</span>
            </label>
            <div v-if="userForm.officeBusyEnabled" class="flex items-center gap-2 bg-slate-50 p-2 rounded-xl border border-slate-200">
              <span class="text-slate-600 font-semibold text-[11px]">Từ:</span>
              <input type="time" v-model="userForm.officeBusyStart" class="bg-white border rounded px-2 py-1 font-bold text-center" />
              <span class="text-slate-600 font-semibold text-[11px]">Đến:</span>
              <input type="time" v-model="userForm.officeBusyEnd" class="bg-white border rounded px-2 py-1 font-bold text-center" />
            </div>
          </div>

          </div>

          <!-- Sticky Footer Buttons -->
          <div class="p-4 border-t border-slate-100 bg-white flex items-center justify-end gap-2 shrink-0 shadow-lg">
            <button type="button" @click="showUserModal = false" class="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 font-semibold text-slate-600">
              Hủy
            </button>
            <button type="submit" :disabled="isSavingUser" class="px-5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold transition shadow-sm">
              <span v-if="isSavingUser">Đang lưu...</span>
              <span v-else>Lưu Thành Viên</span>
            </button>
          </div>

        </form>
      </div>
    </div>

    <!-- RESET PIN MODAL -->
    <div 
      v-if="showResetPinModal" 
      class="fixed inset-0 z-60 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm"
      @click.self="showResetPinModal = false"
    >
      <div class="relative w-full max-w-sm bg-white border border-slate-200 shadow-2xl rounded-3xl p-5 space-y-4">
        <div class="flex items-center justify-between border-b border-slate-100 pb-3">
          <h3 class="font-bold text-sm text-slate-900">🔑 Đặt Lại Mã PIN cho {{ resetPinUserName }}</h3>
          <button @click="showResetPinModal = false" class="w-7 h-7 rounded-full bg-slate-100 text-slate-500 font-bold">✕</button>
        </div>

        <form @submit.prevent="submitResetPin" class="space-y-3 text-xs">
          <div>
            <label class="block font-bold text-slate-700 mb-1">Mã PIN mới (4 - 6 số)</label>
            <input 
              type="text" 
              v-model="newPinValue" 
              required
              maxlength="6"
              placeholder="1234"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-bold text-slate-800 text-center tracking-widest text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500/30"
            />
          </div>

          <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
            <button type="button" @click="showResetPinModal = false" class="px-4 py-2 rounded-xl bg-slate-100 font-semibold text-slate-600">
              Hủy
            </button>
            <button type="submit" :disabled="isResettingPin" class="px-5 py-2 rounded-xl bg-indigo-600 text-white font-bold">
              <span v-if="isResettingPin">Đang cập nhật...</span>
              <span v-else>Cập Nhật PIN</span>
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- CREATE / EDIT SERVICE MODAL -->
    <div 
      v-if="showServiceModal" 
      class="fixed inset-0 z-60 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm"
      @click.self="showServiceModal = false"
    >
      <div class="relative w-full max-w-md bg-white border border-slate-200 shadow-2xl rounded-3xl flex flex-col max-h-[85vh] overflow-hidden">
        <div class="p-5 border-b border-slate-100 flex items-center justify-between shrink-0 bg-slate-50/70">
          <h3 class="font-extrabold text-sm text-slate-900">
            {{ isEditingService ? '✏️ Sửa Loại Dịch Vụ' : '➕ Thêm Loại Dịch Vụ Mới' }}
          </h3>
          <button @click="showServiceModal = false" class="w-7 h-7 rounded-full bg-slate-100 text-slate-500 font-bold">✕</button>
        </div>

        <form @submit.prevent="saveService" class="flex-1 min-h-0 flex flex-col overflow-hidden">
          <div class="flex-1 min-h-0 overflow-y-auto p-5 space-y-3.5 text-xs pb-6">
          <!-- Code -->
          <div>
            <label class="block font-bold text-slate-700 mb-1">Mã Dịch Vụ (Code viết hoa, VD: DRONE_LIGHT, FPV)</label>
            <input 
              type="text" 
              v-model="serviceForm.code" 
              required
              :disabled="isEditingService"
              placeholder="VD: DRONE_LIGHT"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-mono font-bold uppercase text-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500/30 disabled:opacity-50"
            />
          </div>

          <!-- Name -->
          <div>
            <label class="block font-bold text-slate-700 mb-1">Tên Dịch Vụ Hiển Thị</label>
            <input 
              type="text" 
              v-model="serviceForm.name" 
              required
              placeholder="VD: Trình diễn drone nghệ thuật"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-bold text-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500/30"
            />
          </div>

          <!-- Icon / Emoji -->
          <div>
            <label class="block font-bold text-slate-700 mb-1">Icon đại diện (Emoji)</label>
            <div class="flex items-center gap-2 mb-2">
              <span class="text-2xl w-10 h-10 rounded-xl bg-slate-100 border border-slate-200 flex items-center justify-center shrink-0">
                {{ serviceForm.icon }}
              </span>
              <input 
                type="text" 
                v-model="serviceForm.icon" 
                placeholder="Emoji..."
                class="w-20 bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-center text-sm focus:outline-none"
              />
              <!-- Quick Emojis -->
              <div class="flex items-center gap-1 overflow-x-auto py-1">
                <button 
                  v-for="e in quickIcons" 
                  :key="e" 
                  type="button"
                  @click="serviceForm.icon = e"
                  class="w-7 h-7 rounded-lg bg-slate-100 hover:bg-slate-200 text-sm flex items-center justify-center shrink-0"
                >
                  {{ e }}
                </button>
              </div>
            </div>
          </div>

          <!-- Description -->
          <div>
            <label class="block font-bold text-slate-700 mb-1">Mô tả chi tiết</label>
            <textarea 
              v-model="serviceForm.description"
              rows="2"
              placeholder="Mô tả tóm tắt về loại hình dịch vụ này..."
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-medium text-slate-800 focus:outline-none"
            ></textarea>
          </div>

          <!-- Active Toggle -->
          <label class="flex items-center gap-2.5 cursor-pointer bg-slate-50 p-2.5 rounded-xl border border-slate-200">
            <input type="checkbox" v-model="serviceForm.isActive" class="w-4 h-4 text-indigo-600 rounded cursor-pointer" />
            <span class="font-bold text-slate-800">Đang hoạt động (cho phép phân công khi tạo Job)</span>
          </label>

          </div>

          <!-- Sticky Footer Buttons -->
          <div class="p-4 border-t border-slate-100 bg-white flex items-center justify-end gap-2 shrink-0 shadow-lg">
            <button type="button" @click="showServiceModal = false" class="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 font-semibold text-slate-600">
              Hủy
            </button>
            <button type="submit" :disabled="isSavingService" class="px-5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold transition shadow-sm">
              <span v-if="isSavingService">Đang lưu...</span>
              <span v-else>Lưu Dịch Vụ</span>
            </button>
          </div>

        </form>
      </div>
    </div>

  </div>
</template>
