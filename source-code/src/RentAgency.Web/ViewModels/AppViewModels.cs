using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RentAgency.Web.Features.Complaints.Application;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Rentals.Application;
using RentAgency.Web.Features.Rentals.Domain;

namespace RentAgency.Web.ViewModels;

public class SearchPageViewModel
{
    public string? Location { get; set; }

    public decimal? MaxPrice { get; set; }

    public int? Rooms { get; set; }

    public IReadOnlyList<ListingCard> Results { get; set; } = [];
}

public class ListingDetailsPageViewModel
{
    public OwnerListingDetails Listing { get; set; } = null!;

    public bool IsAuthenticated { get; set; }

    public bool IsOwner { get; set; }

    public ApplyForRentalViewModel Apply { get; set; } = new();
}

public class ApplyForRentalViewModel
{
    public Guid ListingId { get; set; }

    [Required(ErrorMessage = "Напишите сообщение")]
    [StringLength(4000)]
    [Display(Name = "Сообщение")]
    public string Message { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите дату начала")]
    [DataType(DataType.Date)]
    [Display(Name = "Дата начала")]
    public DateOnly ProposedStart { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    [Required(ErrorMessage = "Укажите дату окончания")]
    [DataType(DataType.Date)]
    [Display(Name = "Дата окончания")]
    public DateOnly ProposedEnd { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
}

public class ListingFormViewModel
{
    public Guid? ListingId { get; set; }

    public int Version { get; set; }

    public bool IsEdit { get; set; }

    [Required(ErrorMessage = "Укажите адрес")]
    [StringLength(300)]
    [Display(Name = "Адрес")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Добавьте описание")]
    [StringLength(4000)]
    [Display(Name = "Описание")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите условия сдачи")]
    [StringLength(2000)]
    [Display(Name = "Условия сдачи")]
    public string RentalTerms { get; set; } = string.Empty;

    [Range(1, 10_000_000, ErrorMessage = "Укажите цену")]
    [Display(Name = "Цена в месяц, ₽")]
    public decimal Price { get; set; }

    [Range(1, 50, ErrorMessage = "Укажите количество комнат")]
    [Display(Name = "Комнат")]
    public int Rooms { get; set; } = 1;

    [Range(1, 10_000, ErrorMessage = "Укажите площадь")]
    [Display(Name = "Площадь, м²")]
    public double Area { get; set; } = 30;

    [DataType(DataType.Date)]
    [Display(Name = "Доступно с")]
    public DateOnly AvailableFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Display(Name = "Фотографии")]
    public List<IFormFile>? Photos { get; set; }

    public IReadOnlyList<string> ExistingPhotoKeys { get; set; } = [];
}

public class OwnerDashboardViewModel
{
    public IReadOnlyList<OwnerListingDetails> Listings { get; set; } = [];

    public IReadOnlyList<OwnerRequestSummary> Requests { get; set; } = [];
}

public class RequestListViewModel
{
    public IReadOnlyList<OwnerRequestSummary> AsTenant { get; set; } = [];

    public IReadOnlyList<OwnerRequestSummary> AsOwner { get; set; } = [];
}

public class RentalWorkspaceViewModel
{
    public RentalRequestView Request { get; set; } = null!;

    public string Address { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    public Guid CurrentUserId { get; set; }

    public bool IsOwner { get; set; }

    public string? OtherPartyName { get; set; }

    public AgreementView? Agreement { get; set; }

    public IReadOnlyList<ReviewView> Reviews { get; set; } = [];

    public bool HasReview { get; set; }

    public RentalTermsInput Terms { get; set; } = new();
}

public class RentalTermsInput
{
    [DataType(DataType.Date)]
    [Display(Name = "Начало")]
    public DateOnly Start { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    [DataType(DataType.Date)]
    [Display(Name = "Окончание")]
    public DateOnly End { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));

    [Range(1, 10_000_000, ErrorMessage = "Укажите цену")]
    [Display(Name = "Цена в месяц, ₽")]
    public decimal MonthlyPrice { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Залог не может быть отрицательным")]
    [Display(Name = "Залог, ₽")]
    public decimal Deposit { get; set; }
}

public class RentalTermsFormInput : RentalTermsInput
{
    public Guid RequestId { get; set; }
}

public class ComplaintCreateViewModel
{
    public Guid? ListingId { get; set; }

    public Guid? TargetUserId { get; set; }

    public string TargetLabel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Опишите проблему")]
    [StringLength(4000)]
    [Display(Name = "Что произошло")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Материалы")]
    public List<IFormFile>? Evidence { get; set; }
}

public class ComplaintDetailsPageViewModel
{
    public ComplaintDetails Complaint { get; set; } = null!;

    public string TargetLabel { get; set; } = string.Empty;
}

public class ModerationComplaintPageViewModel
{
    public ComplaintDetails Complaint { get; set; } = null!;

    public string TargetLabel { get; set; } = string.Empty;
}

public class ModerationDetailsPageViewModel
{
    public ModerationView Moderation { get; set; } = null!;
}

public class ModerationDecisionInput
{
    [Required]
    public string Result { get; set; } = "Approved";

    [StringLength(2000)]
    [Display(Name = "Замечания")]
    public string? Remarks { get; set; }

    public int ExpectedVersion { get; set; }
}
