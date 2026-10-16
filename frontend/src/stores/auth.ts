import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import type { UserPickerDto, UserProfileDto } from '../types';
import { authApi, profileApi } from '../services/api';

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('token'));
  const storedUser = localStorage.getItem('currentUser');
  const currentUser = ref<UserProfileDto | null>(storedUser ? JSON.parse(storedUser) : null);
  const teamMembers = ref<UserPickerDto[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const isAuthenticated = computed(() => !!token.value && !!currentUser.value);

  async function fetchTeamMembers() {
    try {
      teamMembers.value = await authApi.getUsers();
    } catch (err: any) {
      console.error('Failed to load team members', err);
    }
  }

  async function login(userId: number, pin: string) {
    loading.value = true;
    error.value = null;
    try {
      const res = await authApi.login(userId, pin);
      token.value = res.token;
      currentUser.value = res.user;
      localStorage.setItem('token', res.token);
      localStorage.setItem('currentUser', JSON.stringify(res.user));
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
    officeBusyEnabled: boolean;
    officeBusyStart: string;
    officeBusyEnd: string;
  }) {
    loading.value = true;
    try {
      const updated = await profileApi.updateProfile(data);
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

  async function addBusyDate(date: string, reason?: string) {
    try {
      const item = await profileApi.addBusyDate(date, reason);
      if (currentUser.value) {
        currentUser.value.customBusyDates.push(item);
        localStorage.setItem('currentUser', JSON.stringify(currentUser.value));
      }
    } catch (err: any) {
      error.value = err.message;
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
    loading,
    error,
    isAuthenticated,
    fetchTeamMembers,
    login,
    fetchCurrentUser,
    updateProfile,
    addBusyDate,
    removeBusyDate,
    logout
  };
});
