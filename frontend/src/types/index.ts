export interface UserPickerDto {
  id: number;
  name: string;
  avatarUrl?: string;
  services: string[];
  isAdmin: boolean;
}

export interface BusyDateItemDto {
  id: number;
  date: string;
  reason?: string;
  startTime?: string;
  endTime?: string;
}

export interface UserProfileDto {
  id: number;
  name: string;
  avatarUrl?: string;
  services: string[];
  isAdmin: boolean;
  officeBusyEnabled: boolean;
  officeBusyStart: string;
  officeBusyEnd: string;
  customBusyDates: BusyDateItemDto[];
}

export interface ServiceTypeDto {
  id: number;
  code: string;
  name: string;
  icon: string;
  description?: string;
  isActive: boolean;
  displayOrder: number;
}

export interface SaveServiceTypeRequest {
  code: string;
  name: string;
  icon: string;
  description?: string;
  isActive: boolean;
  displayOrder: number;
}

export interface AdminUserDto {
  id: number;
  name: string;
  avatarUrl?: string;
  services: string[];
  isAdmin: boolean;
  officeBusyEnabled: boolean;
  officeBusyStart: string;
  officeBusyEnd: string;
}

export interface CreateUserRequest {
  name: string;
  pin: string;
  avatarUrl?: string;
  services: string[];
  isAdmin: boolean;
}

export interface UpdateUserAdminRequest {
  name: string;
  avatarUrl?: string;
  services: string[];
  isAdmin: boolean;
  officeBusyEnabled: boolean;
  officeBusyStart: string;
  officeBusyEnd: string;
}

export interface JobAssignmentDto {
  id: number;
  userId: number;
  userName: string;
  userAvatar?: string;
  castAmount: number | null; // null if masked for other crew members
  isPaid: boolean;
  personalNote: string;
}

export interface JobResponseDto {
  id: number;
  createdByUserId: number;
  createdByUserName: string;
  ownerUserId: number | null;
  ownerUserName: string | null;
  externalClientName: string | null;
  eventDate: string;
  startTime: string;
  endTime: string;
  serviceCode: string;
  locationName: string;
  subAddress: string;
  latitude: number;
  longitude: number;
  clientTotalRevenue: number | null; // null if not owner
  externalWorkersNote: string;
  note: string;
  assignments: JobAssignmentDto[];
  isOwner: boolean;
  isAssigned: boolean;
  canEdit: boolean;
  myCastAmount: number | null;
  myIsPaid: boolean | null;
}

export interface JobAssignmentItemDto {
  userId: number;
  castAmount: number;
  isPaid: boolean;
  personalNote?: string;
}

export interface SaveJobRequest {
  eventDate: string;
  startTime: string;
  endTime: string;
  serviceCode: string;
  locationName: string;
  subAddress: string;
  latitude: number;
  longitude: number;
  ownerUserId: number | null;
  externalClientName?: string | null;
  clientTotalRevenue?: number | null;
  externalWorkersNote?: string;
  note?: string;
  assignments: JobAssignmentItemDto[];
}

export interface AvailabilityItemDto {
  userId: number;
  name: string;
  avatarUrl?: string;
  isBusy: boolean;
  busyType: 'FREE' | 'OFFICE_HOURS' | 'DAY_OFF' | 'CONFLICT_JOB';
  busyLabel: string;
}

export interface ServiceItem {
  code: string;
  name: string;
  icon: string;
}

export const SERVICE_CATALOG: ServiceItem[] = [
  { code: 'BUTTERFLY', name: 'Bướm', icon: '🦋' },
  { code: 'FPV', name: 'FPV', icon: '🚁' },
  { code: 'FLYCAM', name: 'Flycam', icon: '✈️' },
  { code: 'CAMERA', name: 'Quay', icon: '🎥' },
  { code: 'PHOTO', name: 'Chụp', icon: '📸' }
];

export interface SavedVenueDto {
  id: number;
  name: string;
  subAddress?: string;
  latitude: number;
  longitude: number;
  usageCount: number;
}

export interface SaveVenueRequest {
  name: string;
  subAddress?: string;
  latitude: number;
  longitude: number;
}
