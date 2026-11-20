namespace CovStudent.Models;

public static class AuditActions
{
    // Authentification
    public const string LoginSuccess = "LOGIN_SUCCESS";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout = "LOGOUT";
    public const string Register = "REGISTER";

    // Trajets
    public const string TripCreated = "TRIP_CREATED";
    public const string TripUpdated = "TRIP_UPDATED";
    public const string TripCancelled = "TRIP_CANCELLED";
    public const string TripCompleted = "TRIP_COMPLETED";
    public const string TripDeleted = "TRIP_DELETED";

    // Réservations
    public const string ReservationCreated = "RESERVATION_CREATED";
    public const string ReservationConfirmed = "RESERVATION_CONFIRMED";
    public const string ReservationRejected = "RESERVATION_REJECTED";
    public const string ReservationCancelled = "RESERVATION_CANCELLED";

    // Administration
    public const string UserBlocked = "USER_BLOCKED";
    public const string UserUnblocked = "USER_UNBLOCKED";
    public const string RoleChanged = "ROLE_CHANGED";
}