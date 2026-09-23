using System.ComponentModel.DataAnnotations;
using Blood_Donations_Project.ViewModels.Shared;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Blood_Donations_Project.ViewModels.BloodRequests
{
    /// <summary>
    /// Hospital "Request Blood" form (Views/Hospital/RequestBlood.cshtml).
    /// Previously ViewModels.BloodRequestViewModel; field names and validation are unchanged.
    /// The two fields are nullable so the GET form still renders empty inputs
    /// (the page used to be rendered without a model).
    /// </summary>
    public class RequestBloodViewModel
    {
        [Required(ErrorMessage = "Blood type is required")]
        [Display(Name = "Blood Type")]
        public int? BloodTypeId { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 100)]
        [Display(Name = "Quantity (units)")]
        public int? Quantity { get; set; }

        /// <summary>
        /// Dropdown options. Filled by the controller on GET and on invalid POST;
        /// never bound from or validated against the posted form.
        /// </summary>
        [BindNever]
        [ValidateNever]
        public List<BloodTypeOptionViewModel> BloodTypeOptions { get; set; } = new();
    }
}
