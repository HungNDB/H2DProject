using System.Runtime.InteropServices;
using System.Text;
using H2DProject.Models;

namespace H2DProject.Services;

public class PrintService
{
    private readonly IConfiguration _config;

    public PrintService(IConfiguration config) => _config = config;

    public Task PrintReceiptAsync(Order order)
    {
        var printerName = _config["Printer:Name"] ?? "POS-80C";
        var bytes = BuildReceiptBytes(order);
        RawPrinterHelper.SendBytesToPrinter(printerName, bytes);
        return Task.CompletedTask;
    }

    private static byte[] BuildReceiptBytes(Order order)
    {
        byte[] INIT = { 0x1B, 0x40 };
        byte[] CENTER = { 0x1B, 0x61, 0x01 };
        byte[] LEFT = { 0x1B, 0x61, 0x00 };
        byte[] BOLD_ON = { 0x1B, 0x45, 0x01 };
        byte[] BOLD_OFF = { 0x1B, 0x45, 0x00 };
        byte[] BIG_ON = { 0x1B, 0x21, 0x30 };  // Double width + height
        byte[] BIG_OFF = { 0x1B, 0x21, 0x00 };
        byte[] WIDE_ON = { 0x1B, 0x21, 0x20 };  // Chỉ double width
        byte[] WIDE_OFF = { 0x1B, 0x21, 0x00 };
        byte[] NL = { 0x0A };
        byte[] CUT = { 0x1D, 0x56, 0x41, 0x10 };

        // Khi BIG_ON (double width) thì mỗi ký tự chiếm 2 col
        // W=32 col bình thường, W_BIG=16 col khi chữ to
        const int W = 46;
        const int W_BIG = 21;

        var parts = new List<byte[]>();

        parts.Add(INIT);

        // ── Header: CENTER command ESC/POS tự căn giữa ──
        // Dòng 1: "H2D" — chữ to double
        parts.Add(CENTER);
        parts.Add(BIG_ON); parts.Add(BOLD_ON);
        parts.Add(T("H2D")); parts.Add(NL);
        parts.Add(BIG_OFF); parts.Add(BOLD_OFF);
        // Dòng 2: "Coffee & Tea" — chữ thường đậm
        parts.Add(BOLD_ON);
        parts.Add(T("Coffee & Tea")); parts.Add(NL);
        parts.Add(BOLD_OFF);
        parts.Add(T("Hoa don thanh toan")); parts.Add(NL);
        parts.Add(LEFT);
        parts.Add(T(new string('=', W))); parts.Add(NL);

        // ── Thông tin đơn ───────────────────────
        parts.Add(LEFT);
        parts.Add(T($"Don #    : #{order.OrderId}")); parts.Add(NL);
        parts.Add(T($"Ban      : {V(order.Table?.TableNumber ?? "Mang ve")}")); parts.Add(NL);
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

            // Tên món — chữ đậm
            parts.Add(BOLD_ON);
            parts.Add(T(ItemRow(name, $" {item.Quantity} ", $"{subtotal:N0}d", W)));
            parts.Add(NL);
            parts.Add(BOLD_OFF);

            // Topping — thụt vào, chữ thường
            if (item.OrderItemToppings != null && item.OrderItemToppings.Any())
            {
                foreach (var t in item.OrderItemToppings)
                {
                    var tname = V(t.Topping?.Name ?? "");
                    var ttotal = t.Quantity * t.UnitPrice;
                    parts.Add(T(ItemRow(
                        "  + " + tname,
                        $" {t.Quantity} ",
                        $"{ttotal:N0}d",
                        W
                    )));
                    parts.Add(NL);
                }
            }
        }

        // ── Ghi chú ─────────────────────────────
        if (!string.IsNullOrEmpty(order.Note))
        {
            parts.Add(T(new string('-', W))); parts.Add(NL);
            parts.Add(T($"Ghi chu: {V(order.Note)}")); parts.Add(NL);
        }

        // ── Tổng tiền ───────────────────────────
        parts.Add(T(new string('-', W))); parts.Add(NL);
        parts.Add(T(PadRight2("Tam tinh :", $"{order.SubTotal ?? 0:N0}d", W))); parts.Add(NL);
        parts.Add(T(PadRight2("VAT(10%) :", $"{order.Vatamount ?? 0:N0}d", W))); parts.Add(NL);

        if ((order.Discount ?? 0) > 0)
        {
            parts.Add(T(PadRight2("Giam gia :", $"-{order.Discount ?? 0:N0}d", W)));
            parts.Add(NL);
        }

        parts.Add(T(new string('=', W))); parts.Add(NL);

        // Tổng cộng — chữ to double width
        parts.Add(WIDE_ON); parts.Add(BOLD_ON);
        var tongStr = "TONG:";
        var tongVal = $"{order.TotalAmount ?? 0:N0}d";
        parts.Add(T(PadRight2(tongStr, tongVal, W_BIG)));
        parts.Add(NL);
        parts.Add(WIDE_OFF); parts.Add(BOLD_OFF);

        parts.Add(T(new string('=', W))); parts.Add(NL);
        parts.Add(T($"Thanh toan: {V(order.PaymentMethod ?? "Tien mat")}")); parts.Add(NL);

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
        foreach (var p in parts)
        {
            Buffer.BlockCopy(p, 0, result, offset, p.Length);
            offset += p.Length;
        }
        return result;
    }


    // ── Encode sang ASCII ────────────────────────
    private static byte[] T(string s)
        => Encoding.ASCII.GetBytes(V(s));

    // Bỏ dấu tiếng Việt — dùng NFD normalization + map thủ công
    private static string V(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // NFD decompose trước để tách base char + combining marks
        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb1 = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            // Bỏ combining marks (dấu thanh)
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb1.Append(c);
        }
        var r = sb1.ToString();

        // Map thủ công các ký tự đặc biệt còn lại
        r = r
            .Replace("ă", "a").Replace("Ă", "A")
            .Replace("ơ", "o").Replace("Ơ", "O")
            .Replace("ư", "u").Replace("Ư", "U")
            .Replace("đ", "d").Replace("Đ", "D")
            .Replace("â", "a").Replace("Â", "A")
            .Replace("ê", "e").Replace("Ê", "E")
            .Replace("ô", "o").Replace("Ô", "O");

        // Chỉ giữ ký tự ASCII
        var sb2 = new System.Text.StringBuilder();
        foreach (var c in r)
            if (c >= 32 && c < 127) sb2.Append(c);
        return sb2.ToString();
    }

    // ── Layout helpers ───────────────────────────

    private static string Line(char c, int w) => new string(c, w);

    // Căn giữa chuỗi trong width w
    private static string CenterStr(string s, int w)
    {
        if (s.Length >= w) return s;
        var pad = (w - s.Length) / 2;
        return new string(' ', pad) + s;
    }

    // 3 cột: tên | SL căn giữa | giá căn phải
    private static string ItemRow(string name, string qty, string price, int w)
    {
        // Cột giá: 10 ký tự căn phải
        var priceCol = price.PadLeft(10);
        // Cột SL: 4 ký tự căn giữa
        var qtyCol = qty.PadLeft(2).PadRight(4);
        // Cột tên: phần còn lại — KHÔNG cắt thêm, để đủ chỗ
        var nameW = w - qtyCol.Length - priceCol.Length;
        var nameCol = Cut(name, nameW).PadRight(nameW);
        return nameCol + qtyCol + priceCol;
    }

    // 2 cột: label trái | value phải
    private static string PadRight2(string left, string right, int w)
    {
        if (left.Length + right.Length >= w) return left + " " + right;
        return left.PadRight(w - right.Length) + right;
    }

    private static string Cut(string s, int max)
        => s.Length <= max ? s : s[..max];
}

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