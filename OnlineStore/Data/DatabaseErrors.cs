using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace OnlineStore.Data;

public static class DatabaseErrors
{
    public static bool IsUnavailable(Exception exception) => exception is DbException or DbUpdateException
        || exception is InvalidOperationException { InnerException: DbException };
}
