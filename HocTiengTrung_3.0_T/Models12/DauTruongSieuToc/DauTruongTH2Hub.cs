using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class DauTruongTH2Hub : Hub
{
    public async Task JoinRoomGroup(string maPhong)
    {
        if (!string.IsNullOrEmpty(maPhong))
        {
            // Sửa thành AddToGroupAsync
            await Groups.AddToGroupAsync(Context.ConnectionId, maPhong.ToUpper());
        }
    }

    public async Task LeaveRoomGroup(string maPhong)
    {
        if (!string.IsNullOrEmpty(maPhong))
        {
            // Sửa thành RemoveFromGroupAsync
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, maPhong.ToUpper());
        }
    }
}