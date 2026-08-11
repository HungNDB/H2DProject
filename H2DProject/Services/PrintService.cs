using H2DProject.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;

public class PrintService
{
    private readonly IConfiguration _config;
    private static readonly HttpClient _http = new();

    public PrintService(IConfiguration config) => _config = config;

    public async Task PrintReceiptAsync(Order order)
    {
        var printerName = _config["Printer:Name"] ?? "POS-80C";
        var storeName = _config["StoreSettings:StoreName"] ?? "H2D";
        var bytes = await BuildReceiptBytesAsync(order, storeName);
        RawPrinterHelper.SendBytesToPrinter(printerName, bytes);
    }

    private static async Task<byte[]> BuildReceiptBytesAsync(Order order, string storeName)
    {
        // ... (giữ nguyên tất cả byte[] INIT, CENTER, LEFT, ... const W = 46 ...)
        byte[] INIT = { 0x1B, 0x40 };
        byte[] CENTER = { 0x1B, 0x61, 0x01 };
        byte[] LEFT = { 0x1B, 0x61, 0x00 };
        byte[] BOLD_ON = { 0x1B, 0x45, 0x01 };
        byte[] BOLD_OFF = { 0x1B, 0x45, 0x00 };
        byte[] BIG_ON = { 0x1B, 0x21, 0x30 };
        byte[] BIG_OFF = { 0x1B, 0x21, 0x00 };
        byte[] WIDE_ON = { 0x1B, 0x21, 0x20 };
        byte[] WIDE_OFF = { 0x1B, 0x21, 0x00 };
        byte[] NL = { 0x0A };
        byte[] CUT = { 0x1D, 0x56, 0x41, 0x10 };
        const int W = 46;
        const int W_BIG = 21;

        var parts = new List<byte[]>();

        parts.Add(INIT);

        // ── Header ──────────────────────────────
        parts.Add(CENTER);
        parts.Add(BIG_ON); parts.Add(BOLD_ON);
        parts.Add(T(storeName)); parts.Add(NL);
        parts.Add(BIG_OFF); parts.Add(BOLD_OFF);
        parts.Add(BOLD_ON);
        parts.Add(T("Coffee & Tea")); parts.Add(NL);
        parts.Add(BOLD_OFF);
        parts.Add(T("Hoa don thanh toan")); parts.Add(NL);
        parts.Add(LEFT);
        parts.Add(T(new string('=', W))); parts.Add(NL);

        // ── Thông tin đơn ───────────────────────
        parts.Add(WIDE_ON); parts.Add(BOLD_ON);
        parts.Add(T($"Don : #{order.OrderId}")); parts.Add(NL);
        parts.Add(T($"Ban : {V(order.Table?.TableNumber ?? "Mang ve")}")); parts.Add(NL);
        parts.Add(WIDE_OFF); parts.Add(BOLD_OFF);
        parts.Add(T($"Nhan vien: {V(order.Staff?.FullName ?? "")}")); parts.Add(NL);
        parts.Add(T($"Thoi gian: {order.CreatedAt?.ToString("HH:mm dd/MM/yyyy") ?? ""}")); parts.Add(NL);
        parts.Add(T(new string('-', W))); parts.Add(NL);

        // ── Header cột ──────────────────────────
        parts.Add(BOLD_ON);
        parts.Add(T(ItemRow("Ten mon", "SL", "Thanh tien", W))); parts.Add(NL);
        parts.Add(BOLD_OFF);
        parts.Add(T(new string('-', W))); parts.Add(NL);

        // ── Danh sách món ───────────────────────
        foreach (var item in order.OrderItems)
        {
            var name = V(item.Product?.Name ?? "");
            var subtotal = item.Quantity * item.UnitPrice;

            parts.Add(BOLD_ON);
            parts.Add(T(ItemRow(name, $" {item.Quantity} ", $"{subtotal:N0}d", W)));
            parts.Add(NL);
            parts.Add(BOLD_OFF);

            if (!string.IsNullOrEmpty(item.Note))
            {
                parts.Add(T($"  ({V(item.Note)})")); parts.Add(NL);
            }

            if (item.OrderItemToppings != null && item.OrderItemToppings.Any())
            {
                foreach (var t in item.OrderItemToppings)
                {
                    var tname = "  + " + V(t.Topping?.Name ?? "");
                    var ttotal = t.Quantity * t.UnitPrice;
                    parts.Add(T(ItemRow(tname, $" {t.Quantity} ", $"{ttotal:N0}d", W)));
                    parts.Add(NL);
                }
            }

            parts.Add(T(new string('.', W))); parts.Add(NL);
        }

        // ── Ghi chú ─────────────────────────────
        if (!string.IsNullOrEmpty(order.Note))
        {
            parts.Add(T(new string('-', W))); parts.Add(NL);
            parts.Add(T($"Ghi chu: {V(order.Note)}")); parts.Add(NL);
        }

        // Đếm tổng số lượng món
        var totalQty = order.OrderItems.Sum(i => i.Quantity);

        // ── Tổng tiền ───────────────────────────
        parts.Add(T(new string('-', W))); parts.Add(NL);
        parts.Add(T(PadRight2("Tong so luong :", $"{totalQty}", W))); parts.Add(NL);
        parts.Add(T(PadRight2("Tam tinh :", $"{order.SubTotal ?? 0:N0}d", W))); parts.Add(NL);
        parts.Add(T(PadRight2("VAT(10%) :", $"{order.Vatamount ?? 0:N0}d", W))); parts.Add(NL);

        if ((order.Discount ?? 0) > 0)
        {
            parts.Add(T(PadRight2("Giam gia :", $"-{order.Discount ?? 0:N0}d", W)));
            parts.Add(NL);
        }

        parts.Add(T(new string('=', W))); parts.Add(NL);

        parts.Add(WIDE_ON); parts.Add(BOLD_ON);
        parts.Add(T(PadRight2("TONG:", $"{order.TotalAmount ?? 0:N0}d", W_BIG)));
        parts.Add(NL);
        parts.Add(WIDE_OFF); parts.Add(BOLD_OFF);

        parts.Add(T(new string('=', W))); parts.Add(NL);
        parts.Add(T($"Thanh toan: {V(order.PaymentMethod ?? "Tien mat")}")); parts.Add(NL);

        // ── QR VietQR ───────────────────────────
        parts.Add(T(new string('-', W))); parts.Add(NL);
        parts.Add(CENTER);
        parts.Add(T("Quet QR de thanh toan:")); parts.Add(NL);
        parts.Add(T("Vietcombank - 0081001280745")); parts.Add(NL);

        try
        {
            var amount = (long)(order.TotalAmount ?? 0);
            var description = Uri.EscapeDataString($"Don #{order.OrderId}");
            var accountName = Uri.EscapeDataString(storeName);
            var qrUrl = $"https://img.vietqr.io/image/VCB-0081001280745-compact.png" +
                              $"?amount={amount}&addInfo={description}&accountName={accountName}";

            var qrBytes = await _http.GetByteArrayAsync(qrUrl);
            var qrEsc = ConvertImageToEscPos(qrBytes, printWidthPx: 384); // 58mm ≈ 384px
            parts.Add(qrEsc);
            parts.Add(NL);
        }
        catch
        {
            parts.Add(T("[Khong tai duoc QR]")); parts.Add(NL);
        }

        // ── Footer ──────────────────────────────
        parts.Add(LEFT);
        parts.Add(T(new string('-', W))); parts.Add(NL);
        parts.Add(CENTER);
        parts.Add(T("Cam on quy khach!")); parts.Add(NL);
        parts.Add(T("Hen gap lai!")); parts.Add(NL);
        parts.Add(T(DateTime.Now.ToString("HH:mm dd/MM/yyyy"))); parts.Add(NL);
        parts.Add(NL); parts.Add(NL); parts.Add(NL); parts.Add(NL);
        parts.Add(CUT);

        var total = parts.Sum(p => p.Length);
        var result = new byte[total];
        var offset = 0;
        foreach (var p in parts) { Buffer.BlockCopy(p, 0, result, offset, p.Length); offset += p.Length; }
        return result;
    }

    // ── Convert PNG → ESC/POS raster (GS v 0) ──
    private static byte[] ConvertImageToEscPos(byte[] imageBytes, int printWidthPx = 384)
    {
        using var ms = new MemoryStream(imageBytes);
        using var src = new Bitmap(ms);

        // Resize về đúng chiều rộng máy in, giữ tỉ lệ
        var ratio = (double)printWidthPx / src.Width;
        var height = (int)(src.Height * ratio);
        using var bmp = new Bitmap(src, printWidthPx, height);

        // ESC/POS yêu cầu width chia hết cho 8
        var widthBytes = (printWidthPx + 7) / 8;

        var cmd = new List<byte>();
        // GS v 0 — raster bit image
        // m=0 (normal), xL/xH = widthBytes, yL/yH = height
        cmd.Add(0x1D); cmd.Add(0x76); cmd.Add(0x30); cmd.Add(0x00);
        cmd.Add((byte)(widthBytes & 0xFF));
        cmd.Add((byte)((widthBytes >> 8) & 0xFF));
        cmd.Add((byte)(height & 0xFF));
        cmd.Add((byte)((height >> 8) & 0xFF));

        for (int y = 0; y < height; y++)
        {
            for (int xByte = 0; xByte < widthBytes; xByte++)
            {
                byte b = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    int x = xByte * 8 + bit;
                    if (x < printWidthPx)
                    {
                        var pixel = bmp.GetPixel(x, y);
                        // Chuyển sang grayscale — pixel tối = in (bit=1)
                        var gray = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                        if (gray < 128)
                            b |= (byte)(0x80 >> bit);
                    }
                }
                cmd.Add(b);
            }
        }

        return cmd.ToArray();
    }

    // ── Giữ nguyên các helper cũ ────────────────
    private static byte[] T(string s) => Encoding.ASCII.GetBytes(V(s));

    private static string V(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb1 = new StringBuilder();
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark) sb1.Append(c);
        }
        var r = sb1.ToString()
            .Replace("ă", "a").Replace("Ă", "A").Replace("ơ", "o").Replace("Ơ", "O")
            .Replace("ư", "u").Replace("Ư", "U").Replace("đ", "d").Replace("Đ", "D")
            .Replace("â", "a").Replace("Â", "A").Replace("ê", "e").Replace("Ê", "E")
            .Replace("ô", "o").Replace("Ô", "O");
        var sb2 = new StringBuilder();
        foreach (var c in r) if (c >= 32 && c < 127) sb2.Append(c);
        return sb2.ToString();
    }

    private static string ItemRow(string name, string qty, string price, int w)
    {
        var priceCol = price.PadLeft(10);
        var qtyCol = qty.PadLeft(2).PadRight(4);
        var nameW = w - qtyCol.Length - priceCol.Length;
        var nameCol = Cut(name, nameW).PadRight(nameW);
        return nameCol + qtyCol + priceCol;
    }

    private static string PadRight2(string left, string right, int w)
        => left.Length + right.Length >= w ? left + " " + right
                                           : left.PadRight(w - right.Length) + right;

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
    private static string CenterStr(string s, int w)
        => s.Length >= w ? s : new string(' ', (w - s.Length) / 2) + s;

    // ── Windows Raw Printer API ──────────────────
    public static class RawPrinterHelper
    {
        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA",
            SetLastError = true, CharSet = CharSet.Ansi,
            ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter",
            SetLastError = true, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA",
            SetLastError = true, CharSet = CharSet.Ansi,
            ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level,
            [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter",
            SetLastError = true, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter",
            SetLastError = true, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter",
            SetLastError = true, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter",
            SetLastError = true, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes,
            int dwCount, out int dwWritten);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName = "H2D Receipt";
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile = null;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType = "RAW";
        }

        public static bool SendBytesToPrinter(string printerName, byte[] bytes)
        {
            OpenPrinter(printerName, out var hPrinter, IntPtr.Zero);
            StartDocPrinter(hPrinter, 1, new DOCINFOA());
            StartPagePrinter(hPrinter);

            var ptr = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            WritePrinter(hPrinter, ptr, bytes.Length, out _);
            Marshal.FreeCoTaskMem(ptr);

            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);
            ClosePrinter(hPrinter);
            return true;
        }
    }
}