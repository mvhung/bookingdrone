namespace EventMap.Api.DTOs;

public record UserPickerDto(int Id, string Name, string? AvatarUrl, List<string> Services, bool IsAdmin);

public record LoginRequest(int? UserId, string? UserName, string Pin);

public record LoginResponse(string Token, UserProfileDto User);

public record UserProfileDto(
    int Id,
    string Name,
    string? AvatarUrl,
    List<string> Services,
    bool IsAdmin,
    bool OfficeBusyEnabled,
    string OfficeBusyStart,
    string OfficeBusyEnd,
    List<BusyDateItemDto> CustomBusyDates
);

public record BusyDateItemDto(int Id, string Date, string? Reason, string? StartTime = null, string? EndTime = null);

public record UpdateProfileRequest(
    List<string> Services,
    bool OfficeBusyEnabled,
    string OfficeBusyStart,
    string OfficeBusyEnd
);

public record AddBusyDateRequest(string Date, string? Reason, string? StartTime = null, string? EndTime = null);

public record ServiceTypeDto(
    int Id,
    string Code,
    string Name,
    string Icon,
    string? Description,
    bool IsActive,
    int DisplayOrder
);

public record SaveServiceTypeRequest(
    string Code,
    string Name,
    string Icon,
    string? Description = null,
    bool IsActive = true,
    int DisplayOrder = 0
);

public record AdminUserDto(
    int Id,
    string Name,
    string? AvatarUrl,
    List<string> Services,
    bool IsAdmin,
    bool OfficeBusyEnabled,
    string OfficeBusyStart,
    string OfficeBusyEnd
);

public record CreateUserRequest(
    string Name,
    string Pin,
    string? AvatarUrl,
    List<string> Services,
    bool IsAdmin = false
);

public record UpdateUserAdminRequest(
    string Name,
    string? AvatarUrl,
    List<string> Services,
    bool IsAdmin,
    bool OfficeBusyEnabled,
    string OfficeBusyStart,
    string OfficeBusyEnd
);

public record ResetPinRequest(string NewPin);

public record AvailabilityItemDto(
    int UserId,
    string Name,
    string? AvatarUrl,
    bool IsBusy,
    string BusyType,
    string BusyLabel
);

public record JobAssignmentDto(
    int Id,
    int UserId,
    string UserName,
    string? UserAvatar,
    decimal? CastAmount,
    bool IsPaid,
    string PersonalNote
);

public record JobResponseDto(
    int Id,
    int CreatedByUserId,
    string CreatedByUserName,
    int? OwnerUserId,
    string? OwnerUserName,
    string? ExternalClientName,
    string EventDate,
    string StartTime,
    string EndTime,
    string ServiceCode,
    string LocationName,
    string SubAddress,
    decimal Latitude,
    decimal Longitude,
    decimal? ClientTotalRevenue,
    string ExternalWorkersNote,
    string Note,
    List<JobAssignmentDto> Assignments,
    bool IsOwner,
    bool IsAssigned,
    bool CanEdit,
    decimal? MyCastAmount,
    bool? MyIsPaid
);

public record JobAssignmentItemDto(
    int UserId,
    decimal CastAmount,
    bool IsPaid,
    string? PersonalNote
);

public record SaveJobRequest(
    string EventDate,
    string StartTime,
    string EndTime,
    string ServiceCode,
    string LocationName,
    string? SubAddress = null,
    decimal Latitude = 0,
    decimal Longitude = 0,
    int? OwnerUserId = null,
    string? ExternalClientName = null,
    decimal? ClientTotalRevenue = null,
    string? ExternalWorkersNote = null,
    string? Note = null,
    List<JobAssignmentItemDto>? Assignments = null
);
