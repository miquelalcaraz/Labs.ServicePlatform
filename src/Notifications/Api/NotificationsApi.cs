namespace QltSystem.Notifications.Api
{
    public static class NotificationsApi
    {
        public static RouteGroupBuilder MapNotificationsApiV1(this IEndpointRouteBuilder app)
        {
            var api = app.MapGroup("api/notifications").HasApiVersion(1.0);
            return api;
        }
    }
}
