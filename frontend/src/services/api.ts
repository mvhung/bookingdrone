import type {
  UserPickerDto,
  UserProfileDto,
  JobResponseDto,
  SaveJobRequest,
  AvailabilityItemDto,
  BusyDateItemDto,
  ServiceTypeDto,
  SaveServiceTypeRequest,
  AdminUserDto,
  CreateUserRequest,
  UpdateUserAdminRequest,
  SavedVenueDto,
  SaveVenueRequest
} from '../types';

const API_BASE = '/api';

function getHeaders(): HeadersInit {
  const token = localStorage.getItem('token');
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  return headers;
}

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const url = `${API_BASE}${endpoint}`;
  const res = await fetch(url, {
    ...options,
    headers: {
      ...getHeaders(),
      ...(options.headers || {})
    }
  });

  if (!res.ok) {
    if (res.status === 401) {
      // Token expired or invalid
      localStorage.removeItem('token');
      localStorage.removeItem('currentUser');
      window.dispatchEvent(new Event('auth:unauthorized'));
    }
    const errBody = await res.json().catch(() => ({ message: res.statusText }));
    throw new Error(errBody.message || `Request failed with status ${res.status}`);
  }

  // Handle 204 No Content
  if (res.status === 204) {
    return {} as T;
  }

  return res.json();
}

export const authApi = {
  getUsers(): Promise<UserPickerDto[]> {
    return request<UserPickerDto[]>('/auth/users');
  },
  login(credentials: { userId?: number; userName?: string; pin: string }): Promise<{ token: string; user: UserProfileDto }> {
    return request<{ token: string; user: UserProfileDto }>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(credentials)
    });
  },
  getMe(): Promise<UserProfileDto> {
    return request<UserProfileDto>('/auth/me');
  },
  changePin(payload: { oldPin?: string; newPin: string }): Promise<{ message: string }> {
    return request<{ message: string }>('/auth/change-pin', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  }
};

export const jobsApi = {
  getJobs(month?: string, date?: string): Promise<JobResponseDto[]> {
    const params = new URLSearchParams();
    if (month) params.append('month', month);
    if (date) params.append('date', date);
    const query = params.toString() ? `?${params.toString()}` : '';
    return request<JobResponseDto[]>(`/jobs${query}`);
  },
  getJob(id: number): Promise<JobResponseDto> {
    return request<JobResponseDto>(`/jobs/${id}`);
  },
  createJob(job: SaveJobRequest): Promise<JobResponseDto> {
    return request<JobResponseDto>('/jobs', {
      method: 'POST',
      body: JSON.stringify(job)
    });
  },
  updateJob(id: number, job: SaveJobRequest): Promise<JobResponseDto> {
    return request<JobResponseDto>(`/jobs/${id}`, {
      method: 'PUT',
      body: JSON.stringify(job)
    });
  },
  deleteJob(id: number): Promise<void> {
    return request<void>(`/jobs/${id}`, {
      method: 'DELETE'
    });
  }
};

export const availabilityApi = {
  check(
    date: string,
    start: string,
    end: string,
    service: string,
    ignoreJobId?: number | null
  ): Promise<AvailabilityItemDto[]> {
    const params = new URLSearchParams({
      date,
      start,
      end,
      service
    });
    if (ignoreJobId) {
      params.append('ignoreJobId', ignoreJobId.toString());
    }
    return request<AvailabilityItemDto[]>(`/availability?${params.toString()}`);
  }
};

export const profileApi = {
  getProfile(): Promise<UserProfileDto> {
    return request<UserProfileDto>('/profile');
  },
  updateProfile(data: {
    services: string[];
    officeBusyEnabled: boolean;
    officeBusyStart: string;
    officeBusyEnd: string;
  }): Promise<UserProfileDto> {
    return request<UserProfileDto>('/profile', {
      method: 'PUT',
      body: JSON.stringify(data)
    });
  },
  addBusyDate(date: string, reason?: string, startTime?: string, endTime?: string): Promise<BusyDateItemDto> {
    return request<BusyDateItemDto>('/profile/busy-dates', {
      method: 'POST',
      body: JSON.stringify({ date, reason, startTime, endTime })
    });
  },
  removeBusyDate(id: number): Promise<void> {
    return request<void>(`/profile/busy-dates/${id}`, {
      method: 'DELETE'
    });
  }
};

export const servicesApi = {
  getServices(includeInactive = false): Promise<ServiceTypeDto[]> {
    return request<ServiceTypeDto[]>(`/services?includeInactive=${includeInactive}`);
  },
  createService(payload: SaveServiceTypeRequest): Promise<ServiceTypeDto> {
    return request<ServiceTypeDto>('/services', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  updateService(id: number, payload: SaveServiceTypeRequest): Promise<ServiceTypeDto> {
    return request<ServiceTypeDto>(`/services/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    });
  },
  deleteService(id: number): Promise<{ message?: string } | void> {
    return request<{ message?: string } | void>(`/services/${id}`, {
      method: 'DELETE'
    });
  }
};

export const adminApi = {
  getUsers(): Promise<AdminUserDto[]> {
    return request<AdminUserDto[]>('/admin/users');
  },
  createUser(payload: CreateUserRequest): Promise<AdminUserDto> {
    return request<AdminUserDto>('/admin/users', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  updateUser(id: number, payload: UpdateUserAdminRequest): Promise<AdminUserDto> {
    return request<AdminUserDto>(`/admin/users/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    });
  },
  resetPin(id: number, newPin: string): Promise<{ message: string }> {
    return request<{ message: string }>(`/admin/users/${id}/reset-pin`, {
      method: 'PUT',
      body: JSON.stringify({ newPin })
    });
  },
  deleteUser(id: number): Promise<void> {
    return request<void>(`/admin/users/${id}`, {
      method: 'DELETE'
    });
  }
};

export const venuesApi = {
  getVenues(): Promise<SavedVenueDto[]> {
    return request<SavedVenueDto[]>('/venues');
  },
  createVenue(payload: SaveVenueRequest): Promise<SavedVenueDto> {
    return request<SavedVenueDto>('/venues', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  updateVenue(id: number, payload: SaveVenueRequest): Promise<SavedVenueDto> {
    return request<SavedVenueDto>(`/venues/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    });
  },
  deleteVenue(id: number): Promise<void> {
    return request<void>(`/venues/${id}`, {
      method: 'DELETE'
    });
  }
};
