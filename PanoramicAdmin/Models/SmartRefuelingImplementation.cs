namespace PanoramicAdmin.Models;

public sealed class SmartRefuelingImplementation
{
    public int Id { get; set; }
    public required string Site { get; set; }
    public required string Activity { get; set; }
    public string? Status2026Q1 { get; set; }
    public string? Status2026Q2 { get; set; }
    public string? Status2026Q3 { get; set; }
    public string? Status2026Q4 { get; set; }
    public string? Status2027Q1 { get; set; }
    public string? Status2027Q2 { get; set; }
    public string? Status2027Q3 { get; set; }
    public string? Status2027Q4 { get; set; }
}

public sealed class SmartRefuelingEquipmentActual
{
    public int Id { get; set; }
    public required string Site { get; set; }
    public int? PitStop { get; set; }
    public int? FuelTruck { get; set; }
    public int? TagAntiMetal { get; set; }
    public int? TagSticker { get; set; }
    public int? TagSegel { get; set; }
    public string? KetPitStop { get; set; }
    public string? KetFuelTruck { get; set; }
    public string? KetTagAntiMetal { get; set; }
    public string? KetTagSticker { get; set; }
    public string? KetTagSegel { get; set; }
}

public sealed class SmartRefuelingCurveProgress
{
    public int Id { get; set; }
    public required string Site { get; set; }
    public required string ItemKey { get; set; }
    public int Percent { get; set; }
    public DateTime UpdatedOn { get; set; }
}

public sealed record CurveGroup(string Title, IReadOnlyList<string> Items);

public sealed record CurvePhase(string Name, IReadOnlyList<CurveGroup> Groups)
{
    public IEnumerable<string> LeafKeys(int phaseIndex) =>
        Groups.SelectMany((group, groupIndex) =>
            Enumerable.Range(0, Math.Max(group.Items.Count, 1))
                .Select(itemIndex => SmartRefuelingCurveCatalog.Key(phaseIndex, groupIndex, itemIndex)));
}

public static class SmartRefuelingCurveCatalog
{
    public static string Key(int phase, int group, int item) => $"{phase + 1}.{group + 1}.{item + 1}";

    public static IReadOnlyList<CurvePhase> Phases { get; } =
    [
        new("Assesment",
        [
            new("1 Presentasi Project", ["Presentasi Materi Project"]),
            new("2 Pembentukan Tim", ["Struktur Organisasi Project"]),
            new("3 Mapping Project Scope", ["Jumlah Storage", "Tipe Storage", "Lokasi Storage"]),
            new("4 Mapping Infrastruktur Sinyal", ["UAT Sinyal Lokasi Storage"]),
            new("5 Persiapan Perangkat Auto CN",
                ["Perangkat Instalasi Auto CN Fix Storage", "Perangkat Instalasi Auto CN Mobile Storage"]),
            new("6 Update Firmware", ["Update Firmware Iflash", "Update Firmware Device IOT Lain"]),
            new("7 Persiapan Dokumen Change Management", []),
            new("8 Sosialisasi dan Campaign",
                ["Sosialisasi ke User (Fuelman & Operartor FT)", "Sosialisasi ke Operator unit"])
        ]),
        new("Pre Instalasi",
        [
            new("1 Technical Sharing",
            [
                "Koordinasi dengan team teknis (IT, PLANT, GS Instrumen, Engineering)",
                "Team HO memberikan sharing mekanisme instalasi berdasarkan technical design yang sudah disepakati saat Assesment"
            ]),
            new("2 Part & Team Preparation",
            [
                "Mempersiapkan Working team",
                "Membuat rencana kerja sesuai timeframe instalasi",
                "Menyiapkan Part sesuai BOM di area kerja yang ditentukan"
            ]),
            new("Instalasi",
            [
                "Update Aplikasi Iflash Auto CN/HM",
                "Instalasi RFID Reader",
                "Instalasi Antena RFID",
                "Wiring RFID Reader & Antena",
                "Pemasangan RFID Tag",
                "Update Firmware Ewacs Pro",
                "Setup software Auto CN & HM (Form Comissioning)",
                "Test konektifitas Auto CN & HM (Form Comissioning)"
            ]),
            new("Persiapan RFID Reader",
            [
                "Extend penyambungan kabel bawaan pabrik ke ke kabel awg (panjang menyesuaikan)",
                "Pengupasan kabel & penyambungan (solder) dan penutupan kabel jacket dengan heatsrink",
                "Pengetesan power 12v rfid reader",
                "Pengetesan Port comunication serial ke mini pc dan umpc",
                "Pengetesan rfid reader dan tag coverage jarak"
            ])
        ]),
        new("Trial",
        [
            new("1 Trial Transaksi",
            [
                "Transaksi Menggunakan Auto CN & HM",
                "Apa yang harus dilakukan jika terjadi Error"
            ]),
            new("C.1 Error CN tidak terbaca",
            [
                "Pastikan RFID reader berfungsi dengan lampu indikator menyala dan membaca dengan indikator bunyi \"tit\" berkali kali",
                "Jika masih tidak terbaca, pastikan RFID tag berada pada radius baca RFID reader",
                "Jika RFID reader tidak menyala, lakukan pencatatan CN dengan manual pada aplikasi",
                "Informasikan ke GL untuk dilakukan follow up pengecekan RFID reader ke departemen terkait"
            ]),
            new("C.2 Error HM tidak terbaca",
            [
                "Jika HM tidak terbaca otomatis, lakukan pencatatan secara manual",
                "Informasikan ke GL untuk dilakukan follow up pengecekan ke departemen terkait"
            ])
        ]),
        new("Implementasi",
        [
            new("1 Implementasi Transaksi",
            [
                "Transaksi Menggunakan Auto CN & HM",
                "Pencatatan manual hanya dilakukan jika ada problem yang belum solved"
            ]),
            new("C.1 Error CN tidak terbaca",
            [
                "Pastikan RFID reader berfungsi dengan lampu indikator menyala dan membaca dengan indikator bunyi \"tit\" berkali kali",
                "Jika masih tidak terbaca, pastikan RFID tag berada pada radius baca RFID reader",
                "Jika RFID reader tidak menyala, lakukan pencatatan CN dengan manual pada aplikasi",
                "Informasikan ke GL untuk dilakukan follow up pengecekan RFID reader ke departemen terkait"
            ]),
            new("C.2 Error HM tidak terbaca",
            [
                "Jika HM tidak terbaca otomatis, lakukan pencatatan secara manual",
                "Informasikan ke GL untuk dilakukan follow up pengecekan ke departemen terkait"
            ]),
            new("2 Troubleshooting",
            [
                "Lakukan Pengecekan pada RFID Reader, RFID Antena, dan RFID Tag",
                "Lakukan pengecekan pada Wiring",
                "Pastikan semua software terupdate dengan versi terbaru",
                "Pastikan jaringan/network tidak bermasalah"
            ]),
            new("3 Standarisasi",
            [
                "Standarisasi instalasi perangkat",
                "Standarisasi posisi parkir unit"
            ])
        ]),
        new("Evaluasi",
        [
            new("1 Monitoring Implementasi", ["Dashboard Summary Auto CN & HM"]),
            new("2 Weekly Meeting", []),
            new("3 PICA", []),
            new("4 Audit Evaluasi [ SOP, INK, STD & Sistem ]", [])
        ])
    ];
}

public static class SmartRefuelingImplementationCatalog
{
    public static IReadOnlyList<string> Periods { get; } =
    [
        "2026 Q1",
        "2026 Q2",
        "2026 Q3",
        "2026 Q4",
        "2027 Q1",
        "2027 Q2",
        "2027 Q3",
        "2027 Q4"
    ];

    public static IReadOnlyList<string> Statuses { get; } =
    [
        "Done",
        "Progress",
        "Overdue",
        "Plan",
        "Unplanned"
    ];
}

public sealed record SmartRefuelingProgressMetric(int Implemented, int Total)
{
    public decimal Percentage => Total == 0
        ? 0
        : decimal.Round((decimal)Implemented / Total * 100, 2);
}

public sealed record SmartRefuelingLiveProgress(
    SmartRefuelingProgressMetric PitStop,
    SmartRefuelingProgressMetric FuelTruck,
    SmartRefuelingProgressMetric Rfid)
{
    public double? Achievement
    {
        get
        {
            var applicable = new[] { PitStop, FuelTruck, Rfid }.Where(metric => metric.Total > 0).ToList();
            return applicable.Count == 0 ? null : (double)applicable.Average(metric => metric.Percentage);
        }
    }
}
