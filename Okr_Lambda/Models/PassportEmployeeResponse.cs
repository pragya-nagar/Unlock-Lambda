using System;
using System.Collections.Generic;
using System.Text;

namespace Okr_Lambda.Models
{
    public class PassportEmployeeResponse
    {
        public long EMP_ID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }
        public string MailId { get; set; }
        public long ReportingTo { get; set; }
        public string ReportingToName { get; set; }
        public long? FunctionalReportingTo { get; set; }
        public string FunctionalReportingToName { get; set; }
        public string Gender { get; set; }
        public string DateOfBirth { get; set; }
        public long? DivisionId { get; set; }
        public string DivisionName { get; set; }
        public long? DesignationID { get; set; }
        public string DesignationName { get; set; }
        public long? GradeID { get; set; }
        public string GradeName { get; set; }
        public bool IsActive { get; set; }
        public string ConfirmationDate { get; set; }
        public string DateOfJoining { get; set; }
        public string ContractType { get; set; }
        public string ContractEndDate { get; set; }
        public long EmployeeId { get; set; }
        public string Image { get; set; }
        public string Skype { get; set; }
        public string LinkedIn { get; set; }
        public string Twitter { get; set; }
        public long? OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public long? LocationId { get; set; }
        public string LocationName { get; set; }
        public long? LOBId { get; set; }
        public string LOBName { get; set; }
        public long? FunctionId { get; set; }
        public string FunctionName { get; set; }
        public long? CompetencyId { get; set; }
        public string CompetencyName { get; set; }
        public string Address { get; set; }
        public string EmployementType { get; set; }
        public long? CountryId { get; set; }
        public string CountryName { get; set; }
        public string ConfirmationDueOn { get; set; }
        public string LinkedIndDescription { get; set; }
        public long EmployeeType { get; set; }
        public long AuthenticationTypeId { get; set; }
        public string AuthenticationTypeName { get; set; }
        public List<ContactDetails> ContactList { get; set; }
    }
    public class ContactDetails
    {
        public string ContactInfoDetailsId { get; set; }
        public string LocationId { get; set; }
        public string LocationName { get; set; }
        public string ContactTypeId { get; set; }
        public string ContactTypeName { get; set; }
        public string ContactNumber { get; set; }
        public string Extension { get; set; }
        public string IsDefault { get; set; }

    }
}

