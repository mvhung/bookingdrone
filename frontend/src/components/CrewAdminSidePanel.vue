<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { adminApi, servicesApi } from '../services/api';
import { useAuthStore } from '../stores/auth';
import { useVenuesStore } from '../stores/venues';
import type { AdminUserDto, ServiceTypeDto, SavedVenueDto, CreateUserRequest, UpdateUserAdminRequest, SaveServiceTypeRequest } from '../types';

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'fly-to', place: { lat: number; lng: number; mainName?: string; subAddress?: string; zoomOnly?: boolean }): void;
}>();

const authStore = useAuthStore();
const venuesStore = useVenuesStore();

const activeTab = ref<'venues' | 'users' | 'services'>('venues');
const loading = ref(false);
const users = ref<AdminUserDto[]>([]);
const services = ref<ServiceTypeDto[]>([]);

const avatarSeeds = ['Hung', 'Minh', 'Tuan', 'Hai', 'Long', 'Khanh', 'Viet', 'Tung', 'Felix', 'Jack', 'Leo'];
const quickIcons = ['🦋', '🚁', '✈️', '🎥', '📸', '✨', '📡', '🎬', '🎯', '💡', '🎤', '🎪'];

onMounted(async () => {
  await loadAllData();
});

async function loadAllData() {
  loading.value = true;
  try {
    const [uList, sList] = await Promise.all([
      adminApi.getUsers(),
      servicesApi.getServices(true),
      venuesStore.loadVenues()
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

// Search queries for each tab
const venueSearchText = ref('');
const userSearchText = ref('');
const serviceSearchText = ref('');

// Subview mode for venues: 'list' or 'edit' inside this side panel
const venueViewMode = ref<'list' | 'edit'>('list');
const isSavingVenue = ref(false);
const venueEditSearchQuery = ref('');
const isSearchingEditLocation = ref(false);

onUnmounted(() => {
  venuesStore.stopEditingVenue();
});

// Filtered data computed for each tab
const filteredVenues = computed(() => {
  const q = venueSearchText.value.trim().toLowerCase();
  if (!q) return venuesStore.venues;
  return venuesStore.venues.filter(v => 
    v.name.toLowerCase().includes(q) || 
    (v.subAddress && v.subAddress.toLowerCase().includes(q))
  );
});

const filteredUsers = computed(() => {
  const q = userSearchText.value.trim().toLowerCase();
  if (!q) return users.value;
  return users.value.filter(u => 
    u.name.toLowerCase().includes(q) || 
    u.services.some(s => s.toLowerCase().includes(q))
  );
});

const filteredServices = computed(() => {
  const q = serviceSearchText.value.trim().toLowerCase();
  if (!q) return services.value;
  return services.value.filter(s => 
    s.name.toLowerCase().includes(q) || 
    s.code.toLowerCase().includes(q)
  );
});

function getGoogleMapsDirectionsUrl(v: { latitude: number; longitude: number }): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${v.latitude},${v.longitude}`;
}

// Venue Create / Edit in Side Window
function openCreateVenue() {
  venueViewMode.value = 'edit';
  venuesStore.startEditingVenue();
}

function openEditVenue(v: SavedVenueDto) {
  venueViewMode.value = 'edit';
  venuesStore.startEditingVenue(v);
  emit('fly-to', { lat: v.latitude, lng: v.longitude, zoomOnly: true });
}

function cancelVenueEdit() {
  venuesStore.stopEditingVenue();
  venueViewMode.value = 'list';
}

async function searchEditVenueLocation() {
  const q = venueEditSearchQuery.value.trim();
  if (!q || !venuesStore.editingVenueData) return;
  isSearchingEditLocation.value = true;
  try {
    const res = await fetch(`https://photon.komoot.io/api/?q=${encodeURIComponent(q)}&lat=21.0285&lon=105.8542&limit=5`);
    if (res.ok) {
      const data = await res.json();
      if (data && data.features && data.features.length > 0) {
        const feat = data.features[0];
        const [lng, lat] = feat.geometry.coordinates;
        venuesStore.editingVenueData.latitude = Number(lat.toFixed(6));
        venuesStore.editingVenueData.longitude = Number(lng.toFixed(6));
        if (!venuesStore.editingVenueData.name) {
          venuesStore.editingVenueData.name = feat.properties.name || q;
        }
        if (!venuesStore.editingVenueData.subAddress) {
          venuesStore.editingVenueData.subAddress = feat.properties.street || feat.properties.district || feat.properties.city || '';
        }
        emit('fly-to', { lat, lng, zoomOnly: true });
      } else {
        alert('Không tìm thấy toạ độ cho địa chỉ này');
      }
    }
  } catch (e) {
    console.error('Error searching location', e);
  } finally {
    isSearchingEditLocation.value = false;
  }
}

async function saveVenueForm() {
  if (!venuesStore.editingVenueData) return;
  const data = venuesStore.editingVenueData;
  if (!data.name.trim()) {
    alert('Vui lòng nhập tên địa điểm / tiệc');
    return;
  }
  isSavingVenue.value = true;
  try {
    if (data.id) {
      await venuesStore.updateVenue(data.id, {
        name: data.name.trim(),
        subAddress: data.subAddress.trim(),
        latitude: Number(data.latitude),
        longitude: Number(data.longitude)
      });
    } else {
      await venuesStore.saveVenue({
        name: data.name.trim(),
        subAddress: data.subAddress.trim(),
        latitude: Number(data.latitude),
        longitude: Number(data.longitude)
      });
    }
    venuesStore.stopEditingVenue();
    venueViewMode.value = 'list';
  } catch (err: any) {
    alert('Lỗi lưu địa điểm: ' + (err.message || 'Thử lại'));
  } finally {
    isSavingVenue.value = false;
  }
}

async function handleDeleteVenue(id: number, name: string) {
  if (!confirm(`Bạn có chắc muốn xóa địa điểm / tiệc "${name}" không?`)) return;
  try {
    await venuesStore.deleteVenue(id);
    if (venuesStore.editingVenueData?.id === id) {
      cancelVenueEdit();
    }
  } catch (err: any) {
    alert('Lỗi khi xóa địa điểm: ' + (err.message || 'Thử lại'));
  }
}


// User modal state
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
    pin: '',
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
    alert(err.message || 'Lỗi khi lưu thông tin');
  } finally {
    isSavingUser.value = false;
  }
}

// Quick Reset PIN modal
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
    alert('Mã PIN mới phải có ít nhất 4 số');
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

// Service modal state
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
  } catch (err: any) {
    alert(err.message || 'Lỗi khi lưu dịch vụ');
  } finally {
    isSavingService.value = false;
  }
}

async function toggleServiceStatus(s: ServiceTypeDto) {
  try {
    await servicesApi.updateService(s.id, {
      code: s.code,
      name: s.name,
      icon: s.icon,
      description: s.description || '',
      isActive: !s.isActive,
      displayOrder: s.displayOrder
    });
    await loadAllData();
  } catch (err: any) {
    alert(err.message || 'Lỗi khi đổi trạng thái');
  }
}

function getServiceIcon(code: string) {
  const s = services.value.find(x => x.code === code);
  return s?.icon || '🎯';
}
</script>

<template>
  <aside 
    class="absolute top-0 bottom-0 left-16 sm:left-[68px] z-25 w-96 sm:w-[420px] max-w-[calc(100vw-72px)] bg-white border-r border-slate-200/90 shadow-2xl flex flex-col select-none transition-all animate-in slide-in-from-left duration-250"
    aria-label="Google Maps Crew & Services Side Panel"
  >
    <!-- TOP HEADER -->
    <div class="p-4 border-b border-slate-100 flex items-center justify-between shrink-0">
      <div class="flex items-center gap-2.5">
        <div class="w-8 h-8 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center font-bold text-lg">
          ⚙️
        </div>
        <div>
          <h2 class="text-sm font-extrabold text-slate-900 tracking-tight">Quản Trị Hệ Thống</h2>
          <div class="text-[11px] text-slate-500 font-medium">
            {{ venuesStore.venues.length }} địa điểm • {{ users.length }} thợ • {{ services.length }} dịch vụ
          </div>
        </div>
      </div>

      <button 
        type="button" 
        @click="$emit('close')"
        class="w-8 h-8 rounded-full hover:bg-slate-100 text-slate-400 hover:text-slate-700 flex items-center justify-center text-sm font-bold transition cursor-pointer"
        title="Đóng bảng quản lý"
      >
        ✕
      </button>
    </div>

    <!-- TABS BAR & ADD BUTTON -->
    <div class="px-4 py-2.5 border-b border-slate-100 flex items-center justify-between gap-2 shrink-0">
      <div v-if="activeTab === 'venues' && venueViewMode === 'edit'" class="flex items-center gap-2">
        <button 
          type="button" 
          @click="cancelVenueEdit"
          class="px-2.5 py-1 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs flex items-center gap-1 transition cursor-pointer"
        >
          <span>←</span>
          <span>Danh sách</span>
        </button>
        <span class="font-extrabold text-xs text-slate-800">
          {{ venuesStore.editingVenueData?.id ? 'Sửa Địa Điểm' : 'Thêm Địa Điểm Mới' }}
        </span>
      </div>

      <div v-else class="flex items-center bg-slate-100 p-0.5 rounded-xl text-xs font-semibold">
        <button 
          type="button" 
          @click="activeTab = 'venues'; venueViewMode = 'list'"
          :class="activeTab === 'venues' ? 'bg-white text-indigo-700 font-bold shadow-2xs' : 'text-slate-600 hover:text-slate-900'"
          class="px-2.5 py-1.5 rounded-lg transition cursor-pointer"
        >
          Địa điểm ({{ venuesStore.venues.length }})
        </button>
        <button 
          type="button" 
          @click="activeTab = 'users'"
          :class="activeTab === 'users' ? 'bg-white text-indigo-700 font-bold shadow-2xs' : 'text-slate-600 hover:text-slate-900'"
          class="px-2.5 py-1.5 rounded-lg transition cursor-pointer"
        >
          Thợ ({{ users.length }})
        </button>
        <button 
          type="button" 
          @click="activeTab = 'services'"
          :class="activeTab === 'services' ? 'bg-white text-indigo-700 font-bold shadow-2xs' : 'text-slate-600 hover:text-slate-900'"
          class="px-2.5 py-1.5 rounded-lg transition cursor-pointer"
        >
          Dịch vụ ({{ services.length }})
        </button>
      </div>

      <button 
        v-if="!(activeTab === 'venues' && venueViewMode === 'edit')"
        type="button" 
        @click="activeTab === 'venues' ? openCreateVenue() : (activeTab === 'users' ? openCreateUser() : openCreateService())"
        class="px-3 py-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs flex items-center gap-1 shadow-xs transition active:scale-95 cursor-pointer shrink-0"
      >
        <span>➕</span>
        <span>{{ activeTab === 'venues' ? 'Thêm Địa Điểm' : (activeTab === 'users' ? 'Thêm Thợ' : 'Thêm Dịch Vụ') }}</span>
      </button>
    </div>

    <!-- TAB 0: VENUES -->
    <div v-if="activeTab === 'venues'" class="flex-1 min-h-0 flex flex-col overflow-hidden">
      
      <!-- SUBVIEW 1: IN-PANEL VENUE EDIT / CREATE -->
      <div v-if="venueViewMode === 'edit' && venuesStore.editingVenueData" class="flex-1 overflow-y-auto p-4 space-y-3.5">
        <!-- Interactive Tip Banner -->
        <div class="p-3 bg-indigo-50/80 border border-indigo-200/80 rounded-2xl flex items-start gap-2.5 text-xs text-indigo-900 shadow-2xs">
          <span class="text-base shrink-0">📍</span>
          <div class="leading-relaxed">
            <span class="font-black text-indigo-700">Chọn vị trí trực quan:</span>
            <span class="text-indigo-900/80 block mt-0.5">Click vào bất kỳ điểm nào trên bản đồ lớn phía sau để ghim toạ độ tiệc chính xác.</span>
          </div>
        </div>

        <!-- Quick Photon Search in Side Window -->
        <div>
          <label class="block font-bold text-slate-700 text-xs mb-1">Tìm nhanh theo tên đường / địa danh:</label>
          <div class="flex items-center gap-1.5">
            <div class="relative flex-1">
              <input 
                type="text"
                v-model="venueEditSearchQuery"
                @keydown.enter.prevent="searchEditVenueLocation"
                placeholder="VD: Trống Đồng Palace, 173B Trường Chinh..."
                class="w-full bg-slate-50 border border-slate-200 rounded-xl pl-8 pr-3 py-1.5 text-xs text-slate-800 placeholder-slate-400 focus:outline-none focus:border-indigo-500"
              />
              <span class="absolute left-2.5 top-1.5 text-slate-400 text-xs">🔍</span>
            </div>
            <button 
              type="button" 
              @click="searchEditVenueLocation" 
              :disabled="isSearchingEditLocation || !venueEditSearchQuery.trim()"
              class="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 disabled:opacity-50 text-slate-700 rounded-xl font-bold text-xs shrink-0 transition cursor-pointer"
            >
              {{ isSearchingEditLocation ? 'Đang tìm...' : 'Tìm' }}
            </button>
          </div>
        </div>

        <!-- Venue Name Field -->
        <div>
          <label class="block font-bold text-slate-700 text-xs mb-1">Tên địa điểm / tiệc *</label>
          <input 
            type="text" 
            v-model="venuesStore.editingVenueData.name" 
            required 
            placeholder="VD: JW Marriott Hotel Hanoi, Trống Đồng Cảnh Hồ..." 
            class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-slate-900 font-bold text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
          />
        </div>

        <!-- Sub Address Field -->
        <div>
          <label class="block font-bold text-slate-700 text-xs mb-1">Địa chỉ chi tiết</label>
          <input 
            type="text" 
            v-model="venuesStore.editingVenueData.subAddress" 
            placeholder="VD: Số 8 Đỗ Đức Dục, Nam Từ Liêm, Hà Nội" 
            class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-slate-800 font-medium text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
          />
        </div>

        <!-- Coordinates Status -->
        <div class="p-3 bg-slate-50 rounded-2xl border border-slate-200/80 flex items-center justify-between text-xs">
          <div>
            <span class="text-slate-400 font-medium block text-[10px]">Toạ độ trên bản đồ:</span>
            <span class="font-mono font-bold text-slate-800 text-xs">
              {{ venuesStore.editingVenueData.latitude?.toFixed(5) }}, {{ venuesStore.editingVenueData.longitude?.toFixed(5) }}
            </span>
          </div>
          <a 
            :href="`https://www.google.com/maps/dir/?api=1&destination=${venuesStore.editingVenueData.latitude},${venuesStore.editingVenueData.longitude}`"
            target="_blank"
            rel="noopener noreferrer"
            class="text-indigo-600 hover:text-indigo-800 font-bold text-[11px] flex items-center gap-1"
          >
            <span>Mở Google Maps</span>
            <span>↗</span>
          </a>
        </div>

        <!-- Actions -->
        <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
          <button 
            type="button" 
            @click="cancelVenueEdit" 
            class="px-3.5 py-2 rounded-xl text-slate-600 hover:bg-slate-100 font-bold text-xs cursor-pointer transition"
          >
            Hủy
          </button>
          <button 
            type="button" 
            @click="saveVenueForm" 
            :disabled="isSavingVenue" 
            class="px-5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs shadow-xs transition cursor-pointer"
          >
            <span v-if="isSavingVenue">Đang lưu...</span>
            <span v-else>Lưu Địa Điểm</span>
          </button>
        </div>
      </div>

      <!-- SUBVIEW 2: VENUES COMPACT LIST -->
      <div v-else class="flex-1 flex flex-col min-h-0">
        <!-- Search bar -->
        <div class="p-2.5 border-b border-slate-100 shrink-0">
          <div class="relative">
            <input 
              type="text"
              v-model="venueSearchText"
              placeholder="Tìm kiếm địa điểm theo tên hoặc địa chỉ..."
              class="w-full bg-slate-50 border border-slate-200 rounded-xl pl-8 pr-3 py-1.5 text-xs text-slate-800 placeholder-slate-400 focus:outline-none focus:border-indigo-500"
            />
            <span class="absolute left-2.5 top-1.5 text-slate-400 text-xs">🔍</span>
          </div>
        </div>

        <!-- Venue Rows -->
        <div class="flex-1 overflow-y-auto p-2.5 space-y-1.5">
          <div v-if="filteredVenues.length === 0" class="text-center py-10 text-slate-400 text-xs">
            <div class="text-2xl mb-1">🏛️</div>
            <div class="font-bold text-slate-600">Không tìm thấy địa điểm</div>
            <p class="text-[11px] mt-0.5 text-slate-400">Thử tìm với từ khóa khác hoặc nhấn "Thêm Địa Điểm".</p>
          </div>

          <div 
            v-for="v in filteredVenues" 
            :key="v.id"
            @click="emit('fly-to', { lat: v.latitude, lng: v.longitude, zoomOnly: true })"
            class="group flex items-center justify-between gap-2 px-3 py-2 rounded-xl bg-white hover:bg-indigo-50/50 border border-slate-200/80 hover:border-indigo-300 transition cursor-pointer shadow-2xs"
            title="Nhấn để zoom tới vị trí này trên bản đồ"
          >
            <div class="flex items-center gap-2.5 min-w-0">
              <div class="w-7 h-7 rounded-lg bg-sky-50 text-sky-600 flex items-center justify-center font-bold text-sm shrink-0">
                🏛️
              </div>
              <div class="min-w-0">
                <h3 class="font-bold text-xs text-slate-900 truncate leading-snug group-hover:text-indigo-600 transition">{{ v.name }}</h3>
                <p class="text-[10px] text-slate-400 truncate">{{ v.subAddress || 'Chưa có địa chỉ chi tiết' }}</p>
              </div>
            </div>

            <div class="flex items-center gap-1 shrink-0" @click.stop>
              <a 
                :href="getGoogleMapsDirectionsUrl(v)"
                target="_blank"
                rel="noopener noreferrer"
                class="p-1.5 rounded-lg hover:bg-blue-50 text-slate-400 hover:text-blue-600 transition"
                title="Chỉ đường Google Maps"
              >
                🗺️
              </a>
              <button 
                type="button" 
                @click="openEditVenue(v)"
                class="p-1.5 rounded-lg hover:bg-slate-200 text-slate-400 hover:text-slate-700 transition"
                title="Sửa địa điểm"
              >
                ✏️
              </button>
              <button 
                type="button" 
                @click="handleDeleteVenue(v.id, v.name)"
                class="p-1.5 rounded-lg hover:bg-rose-50 text-slate-400 hover:text-rose-600 transition"
                title="Xóa địa điểm"
              >
                🗑️
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- TAB 1: USERS (THỢ) COMPACT LIST -->
    <div v-else-if="activeTab === 'users'" class="flex-1 min-h-0 flex flex-col overflow-hidden">
      <!-- Search bar -->
      <div class="p-2.5 border-b border-slate-100 shrink-0">
        <div class="relative">
          <input 
            type="text"
            v-model="userSearchText"
            placeholder="Tìm theo tên thợ, kỹ năng..."
            class="w-full bg-slate-50 border border-slate-200 rounded-xl pl-8 pr-3 py-1.5 text-xs text-slate-800 placeholder-slate-400 focus:outline-none focus:border-indigo-500"
          />
          <span class="absolute left-2.5 top-1.5 text-slate-400 text-xs">🔍</span>
        </div>
      </div>

      <!-- User Rows -->
      <div class="flex-1 overflow-y-auto p-2.5 space-y-1.5">
        <div v-if="filteredUsers.length === 0" class="text-center py-10 text-slate-400 text-xs">
          <div class="font-bold text-slate-600">Không tìm thấy thợ phù hợp</div>
        </div>

        <div 
          v-for="u in filteredUsers" 
          :key="u.id"
          class="flex items-center justify-between gap-2 px-3 py-2 rounded-xl bg-white hover:bg-slate-50 border border-slate-200/80 hover:border-indigo-300 transition shadow-2xs"
        >
          <div class="flex items-center gap-2.5 min-w-0">
            <div class="w-8 h-8 rounded-full bg-slate-100 border border-slate-200 overflow-hidden shrink-0">
              <img :src="u.avatarUrl || `https://api.dicebear.com/7.x/bottts/svg?seed=${u.name}`" :alt="u.name" class="w-full h-full object-cover" />
            </div>
            <div class="min-w-0">
              <div class="flex items-center gap-1.5">
                <span class="font-bold text-xs text-slate-900 truncate">{{ u.name }}</span>
                <span v-if="u.isAdmin" class="bg-amber-100 text-amber-800 text-[9px] font-bold px-1.5 py-0.2 rounded-md">Admin</span>
              </div>
              <div class="text-[10px] text-slate-500 truncate flex items-center gap-1">
                <span v-if="u.services.length > 0">{{ u.services.map(s => getServiceIcon(s)).join(' ') }}</span>
                <span v-if="u.officeBusyEnabled" class="text-amber-600 font-medium">• Giờ HC</span>
              </div>
            </div>
          </div>

          <div class="flex items-center gap-1 shrink-0">
            <button 
              type="button" 
              @click="openResetPin(u)"
              class="px-2 py-1 rounded-lg bg-slate-100 hover:bg-amber-50 hover:text-amber-700 text-slate-600 font-bold text-[10px] transition cursor-pointer"
              title="Đổi mã PIN"
            >
              🔑 PIN
            </button>
            <button 
              type="button" 
              @click="openEditUser(u)"
              class="p-1.5 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-600 font-bold text-[10px] transition cursor-pointer"
              title="Sửa thông tin"
            >
              ✏️
            </button>
            <button 
              v-if="u.id !== authStore.currentUser?.id"
              type="button" 
              @click="deleteUser(u)"
              class="p-1.5 rounded-lg bg-slate-100 hover:bg-rose-50 hover:text-rose-600 text-slate-400 font-bold text-[10px] transition cursor-pointer"
              title="Xóa"
            >
              🗑️
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- TAB 2: SERVICES (DỊCH VỤ) COMPACT LIST -->
    <div v-else-if="activeTab === 'services'" class="flex-1 min-h-0 flex flex-col overflow-hidden">
      <!-- Search bar -->
      <div class="p-2.5 border-b border-slate-100 shrink-0">
        <div class="relative">
          <input 
            type="text"
            v-model="serviceSearchText"
            placeholder="Tìm theo tên dịch vụ, mã code..."
            class="w-full bg-slate-50 border border-slate-200 rounded-xl pl-8 pr-3 py-1.5 text-xs text-slate-800 placeholder-slate-400 focus:outline-none focus:border-indigo-500"
          />
          <span class="absolute left-2.5 top-1.5 text-slate-400 text-xs">🔍</span>
        </div>
      </div>

      <!-- Service Rows -->
      <div class="flex-1 overflow-y-auto p-2.5 space-y-1.5">
        <div v-if="filteredServices.length === 0" class="text-center py-10 text-slate-400 text-xs">
          <div class="font-bold text-slate-600">Không tìm thấy dịch vụ</div>
        </div>

        <div 
          v-for="s in filteredServices" 
          :key="s.id"
          class="flex items-center justify-between gap-2 px-3 py-2 rounded-xl bg-white hover:bg-slate-50 border border-slate-200/80 transition shadow-2xs"
        >
          <div class="flex items-center gap-2.5 min-w-0">
            <div class="w-7 h-7 rounded-lg bg-slate-100 flex items-center justify-center text-sm shrink-0">
              {{ s.icon }}
            </div>
            <div class="min-w-0">
              <div class="flex items-center gap-1.5">
                <span class="font-bold text-xs text-slate-900 truncate">{{ s.name }}</span>
                <span class="text-[9px] font-mono text-slate-400 uppercase">[{{ s.code }}]</span>
              </div>
              <p class="text-[10px] text-slate-400 truncate">{{ s.description || 'Chưa có mô tả' }}</p>
            </div>
          </div>

          <div class="flex items-center gap-1 shrink-0">
            <button 
              type="button" 
              @click="toggleServiceStatus(s)"
              :class="s.isActive ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-slate-100 text-slate-400 border-slate-200'"
              class="px-2 py-0.5 rounded-lg border text-[10px] font-bold transition cursor-pointer"
            >
              {{ s.isActive ? 'Bật' : 'Tắt' }}
            </button>
            <button 
              type="button" 
              @click="openEditService(s)"
              class="p-1.5 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-600 font-bold text-[10px] transition cursor-pointer"
              title="Sửa"
            >
              ✏️
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- USER MODAL (CREATE / EDIT) -->
    <div 
      v-if="showUserModal"
      class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 animate-in fade-in"
      @click.self="showUserModal = false"
    >
      <div class="bg-white rounded-3xl shadow-2xl border border-slate-200 w-full max-w-md p-5 space-y-4 max-h-[90vh] overflow-y-auto">
        <div class="flex items-center justify-between border-b border-slate-100 pb-3">
          <h3 class="font-extrabold text-slate-900 text-sm">
            {{ isEditingUser ? 'Chỉnh Sửa Thành Viên' : 'Thêm Thành Viên Mới' }}
          </h3>
          <button type="button" @click="showUserModal = false" class="text-slate-400 hover:text-slate-700 font-bold text-sm">✕</button>
        </div>

        <div class="space-y-3 text-xs">
          <div>
            <label class="block font-bold text-slate-700 mb-1">Tên thành viên:</label>
            <input 
              v-model="userForm.name"
              placeholder="VD: Nguyễn Văn A..."
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-bold text-slate-800 focus:outline-none focus:border-indigo-500"
            />
          </div>

          <div v-if="!isEditingUser">
            <label class="block font-bold text-slate-700 mb-1">Mã PIN khởi tạo (4 chữ số):</label>
            <input 
              v-model="userForm.pin"
              maxlength="4"
              placeholder="1234"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-mono font-bold text-slate-800 focus:outline-none focus:border-indigo-500"
            />
          </div>

          <!-- Services Multi-select -->
          <div>
            <label class="block font-bold text-slate-700 mb-1">Dịch vụ đảm nhận:</label>
            <div class="flex flex-wrap gap-1.5">
              <button 
                v-for="s in services" 
                :key="s.code"
                type="button" 
                @click="toggleUserService(s.code)"
                :class="userForm.services.includes(s.code) ? 'bg-indigo-600 text-white font-bold' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'"
                class="px-2.5 py-1.5 rounded-xl text-xs transition flex items-center gap-1"
              >
                <span>{{ s.icon }}</span>
                <span>{{ s.name }}</span>
              </button>
            </div>
          </div>

          <!-- Role checkbox -->
          <div class="pt-2">
            <label class="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" v-model="userForm.isAdmin" class="rounded text-indigo-600" />
              <span class="font-bold text-slate-800">Cấp quyền Quản trị viên (Admin)</span>
            </label>
          </div>

          <!-- Office Hours -->
          <div class="p-3 bg-slate-50 rounded-2xl border border-slate-100 space-y-2">
            <label class="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" v-model="userForm.officeBusyEnabled" class="rounded text-indigo-600" />
              <span class="font-bold text-slate-800">Lịch làm việc giờ hành chính (T2-T6)</span>
            </label>

            <div v-if="userForm.officeBusyEnabled" class="flex items-center gap-2 pt-1">
              <input type="time" v-model="userForm.officeBusyStart" class="bg-white border border-slate-200 rounded-lg px-2 py-1 text-xs" />
              <span>đến</span>
              <input type="time" v-model="userForm.officeBusyEnd" class="bg-white border border-slate-200 rounded-lg px-2 py-1 text-xs" />
            </div>
          </div>
        </div>

        <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
          <button type="button" @click="showUserModal = false" class="px-4 py-2 rounded-xl text-slate-600 hover:bg-slate-100 font-bold text-xs">
            Hủy
          </button>
          <button 
            type="button" 
            @click="saveUser"
            :disabled="isSavingUser"
            class="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs shadow-xs transition"
          >
            {{ isSavingUser ? 'Đang lưu...' : 'Lưu thành viên' }}
          </button>
        </div>
      </div>
    </div>

    <!-- RESET PIN MODAL -->
    <div 
      v-if="showResetPinModal"
      class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 animate-in fade-in"
      @click.self="showResetPinModal = false"
    >
      <div class="bg-white rounded-3xl shadow-2xl border border-slate-200 w-full max-w-xs p-5 space-y-4">
        <h3 class="font-extrabold text-slate-900 text-sm">
          Đổi PIN cho {{ resetPinUserName }}
        </h3>
        <div>
          <label class="block font-bold text-slate-700 text-xs mb-1">Mã PIN 4 số mới:</label>
          <input 
            v-model="newPinValue" 
            maxlength="4" 
            placeholder="1234"
            class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-center text-lg font-mono font-bold tracking-widest text-slate-800 focus:outline-none focus:border-amber-500" 
          />
        </div>
        <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
          <button type="button" @click="showResetPinModal = false" class="px-3 py-1.5 rounded-xl text-slate-600 hover:bg-slate-100 font-bold text-xs">Hủy</button>
          <button 
            type="button" 
            @click="submitResetPin" 
            :disabled="isResettingPin"
            class="px-4 py-1.5 rounded-xl bg-amber-500 hover:bg-amber-600 text-white font-bold text-xs shadow-xs"
          >
            {{ isResettingPin ? 'Đang đổi...' : 'Xác nhận đổi' }}
          </button>
        </div>
      </div>
    </div>

    <!-- SERVICE MODAL (CREATE / EDIT) -->
    <div 
      v-if="showServiceModal"
      class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 animate-in fade-in"
      @click.self="showServiceModal = false"
    >
      <div class="bg-white rounded-3xl shadow-2xl border border-slate-200 w-full max-w-sm p-5 space-y-4">
        <div class="flex items-center justify-between border-b border-slate-100 pb-3">
          <h3 class="font-extrabold text-slate-900 text-sm">
            {{ isEditingService ? 'Sửa Dịch Vụ' : 'Thêm Dịch Vụ Mới' }}
          </h3>
          <button type="button" @click="showServiceModal = false" class="text-slate-400 hover:text-slate-700 font-bold text-sm">✕</button>
        </div>

        <div class="space-y-3 text-xs">
          <div>
            <label class="block font-bold text-slate-700 mb-1">Mã Code (viết hoa không dấu):</label>
            <input 
              v-model="serviceForm.code"
              :disabled="isEditingService"
              placeholder="VD: DRONE_LIGHT"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-mono font-bold text-slate-800 uppercase focus:outline-none focus:border-indigo-500 disabled:opacity-50"
            />
          </div>

          <div>
            <label class="block font-bold text-slate-700 mb-1">Tên hiển thị:</label>
            <input 
              v-model="serviceForm.name"
              placeholder="VD: Trình diễn Drone"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-bold text-slate-800 focus:outline-none focus:border-indigo-500"
            />
          </div>

          <div>
            <label class="block font-bold text-slate-700 mb-1">Icon đại diện:</label>
            <div class="flex items-center gap-2">
              <input 
                v-model="serviceForm.icon"
                class="w-12 text-center text-xl bg-slate-50 border border-slate-200 rounded-xl py-1.5"
              />
              <div class="flex flex-wrap gap-1 flex-1">
                <button 
                  v-for="ic in quickIcons" 
                  :key="ic" 
                  type="button"
                  @click="serviceForm.icon = ic"
                  class="w-7 h-7 rounded-lg hover:bg-slate-100 flex items-center justify-center text-sm"
                >
                  {{ ic }}
                </button>
              </div>
            </div>
          </div>

          <div>
            <label class="block font-bold text-slate-700 mb-1">Mô tả chi tiết:</label>
            <input 
              v-model="serviceForm.description"
              placeholder="VD: Drone chiếu sáng nghệ thuật sự kiện"
              class="w-full bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 font-medium text-slate-800 focus:outline-none"
            />
          </div>
        </div>

        <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
          <button type="button" @click="showServiceModal = false" class="px-3 py-1.5 rounded-xl text-slate-600 hover:bg-slate-100 font-bold text-xs">Hủy</button>
          <button 
            type="button" 
            @click="saveService"
            :disabled="isSavingService"
            class="px-4 py-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs shadow-xs"
          >
            {{ isSavingService ? 'Đang lưu...' : 'Lưu dịch vụ' }}
          </button>
        </div>
      </div>
    </div>
  </aside>
</template>
