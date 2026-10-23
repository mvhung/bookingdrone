import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import type { UserPickerDto, UserProfileDto, ServiceTypeDto } from '../types';
import { authApi, profileApi, servicesApi } from '../services/api';

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('token'));
  const storedUser = localStorage.getItem('currentUser');
  const currentUser = ref<UserProfileDto | null>(storedUser ? JSON.parse(storedUser) : null);
  const teamMembers = ref<UserPickerDto[]>([]);
  const services = ref<ServiceTypeDto[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const isAuthenticated = computed(() => !!token.value && !!currentUser.value);
  const isAdmin = computed(() => !!currentUser.value?.isAdmin);

  async function fetchServices() {
    try {
      services.value = await servicesApi.getServices();
    } catch (err: any) {
      console.error('Failed to load services', err);
    }
  }

  async function fetchTeamMembers() {
    try {
      teamMembers.value = await authApi.getUsers();
    } catch (err: any) {
      console.error('Failed to load team members', err);
    }
  }

  async function login(nameOrId: string | number, pin: string) {
    loading.value = true;
    error.value = null;
    try {
      const payload = typeof nameOrId === 'number'
        ? { userId: nameOrId, pin }
        : { userName: nameOrId, pin };
      const res = await authApi.login(payload);
      token.value = res.token;
      currentUser.value = res.user;
      localStorage.setItem('token', res.token);
      localStorage.setItem('currentUser', JSON.stringify(res.user));
      await fetchServices();
      await fetchTeamMembers();
      return true;
    } catch (err: any) {
      error.value = err.message || 'Đăng nhập không thành công';
      return false;
    } finally {
      loading.value = false;
    }
  }

  async function fetchCurrentUser() {
    if (!token.value) return;
    try {
      const user = await authApi.getMe();
      currentUser.value = user;
      localStorage.setItem('currentUser', JSON.stringify(user));
    } catch {
      logout();
    }
  }

  async function updateProfile(data: {
    services: string[];
    officeBusyEnabled?: boolean;
    officeBusyStart?: string;
    officeBusyEnd?: string;
  }) {
    loading.value = true;
    try {
      const payload = {
        services: data.services,
        officeBusyEnabled: data.officeBusyEnabled ?? currentUser.value?.officeBusyEnabled ?? false,
        officeBusyStart: data.officeBusyStart ?? currentUser.value?.officeBusyStart ?? '08:00',
        officeBusyEnd: data.officeBusyEnd ?? currentUser.value?.officeBusyEnd ?? '17:30',
      };
      const updated = await profileApi.updateProfile(payload);
      currentUser.value = updated;
      localStorage.setItem('currentUser', JSON.stringify(updated));
      return true;
    } catch (err: any) {
      error.value = err.message;
      return false;
    } finally {
      loading.value = false;
    }
  }

  async function addBusyDate(date: string, reason?: string, startTime?: string, endTime?: string) {
    try {
      const item = await profileApi.addBusyDate(date, reason, startTime, endTime);
      if (currentUser.value) {
        currentUser.value.customBusyDates.push(item);
        localStorage.setItem('currentUser', JSON.stringify(currentUser.value));
      }
      return item;
    } catch (err: any) {
      error.value = err.message;
      throw err;
    }
  }

  async function removeBusyDate(id: number) {
    try {
      await profileApi.removeBusyDate(id);
      if (currentUser.value) {
        currentUser.value.customBusyDates = currentUser.value.customBusyDates.filter(b => b.id !== id);
        localStorage.setItem('currentUser', JSON.stringify(currentUser.value));
      }
    } catch (err: any) {
      error.value = err.message;
      throw err;
    }
  }

  function logout() {
    token.value = null;
    currentUser.value = null;
    localStorage.removeItem('token');
    localStorage.removeItem('currentUser');
  }

  return {
    token,
    currentUser,
    teamMembers,
    services,
    loading,
    error,
    isAuthenticated,
    isAdmin,
    fetchServices,
    fetchTeamMembers,
    login,
    fetchCurrentUser,
    updateProfile,
    addBusyDate,
    removeBusyDate,
    logout
  };
});
