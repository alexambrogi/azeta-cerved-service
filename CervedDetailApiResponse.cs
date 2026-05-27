using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class CervedDetailApiResponse
{
    public Company[] companies { get; set; }
    public Person[] people { get; set; }
}

public class Company
{
    public int subject_id { get; set; }
    public string tax_code { get; set; }
    public string vat_number { get; set; }
    public string name { get; set; }
    public Protest[] protests { get; set; }
    public Prejudicial_Events[] prejudicial_events { get; set; }
    public Procedure[] procedures { get; set; }
    public Crisis_Events[] crisis_events { get; set; }
    public Cigs_Decrees[] cigs_decrees { get; set; }
    public Personal_Bankruptcies[] personal_bankruptcies { get; set; }
}

public class Protest
{
    public Personal_Data personal_data { get; set; }
    public Protests_Registry[] protests_registry { get; set; }
}

public class Personal_Data
{
    public string name { get; set; }
    public string tax_code { get; set; }
    public string residence_province_code { get; set; }
    public string residence_province { get; set; }
    public string residence_municipality { get; set; }
    public string residence_address { get; set; }
    public string birth_date { get; set; }
    public string birth_province_code { get; set; }
    public string birth_municipality { get; set; }
    public string birth_country { get; set; }
    public string birth_place { get; set; }
}

public class Protests_Registry
{
    public string registration_date { get; set; }
    public string amount { get; set; }
    public string currency { get; set; }
    public string raising_date { get; set; }
    public string raising_municipality { get; set; }
    public string register_number { get; set; }
    public string expiry_date { get; set; }
    public string type { get; set; }
    public string type_code { get; set; }
    public string refusal_reason { get; set; }
    public string additional_info { get; set; }
}

public class Prejudicial_Events
{
    public string deed_date { get; set; }
    public string land_agency_type { get; set; }
    public string land_agency { get; set; }
    public string land_agency_province_code { get; set; }
    public string land_agency_municipality { get; set; }
    public string land_agency_municipality_belfiore_code { get; set; }
    public string deed_type_code { get; set; }
    public string deed_description { get; set; }
    public int deed_general_registration_number { get; set; }
    public int deed_specific_registration_number { get; set; }
    public string enrolled_amount { get; set; }
    public int deed_ministerial_code { get; set; }
    public string enrolled_amount_currency_code { get; set; }
    public string capital_amount { get; set; }
    public string capital_amount_currency_code { get; set; }
    public int cerved_final_code { get; set; }
    public string cerved_final_class { get; set; }
    public string normalized_deed_description { get; set; }
    public Charged_Subjects_Personal_Data[] charged_subjects_personal_data { get; set; }
    public Beneficiary_Subjects_Personal_Data[] beneficiary_subjects_personal_data { get; set; }
    public Real_Estate_Details[] real_estate_details { get; set; }
}

public class Charged_Subjects_Personal_Data
{
    public string name { get; set; }
    public string tax_code { get; set; }
    public string birth_province_code { get; set; }
    public string birth_municipality { get; set; }
    public string birth_date { get; set; }
}

public class Beneficiary_Subjects_Personal_Data
{
    public string name { get; set; }
    public string tax_code { get; set; }
    public string birth_province_code { get; set; }
    public string birth_municipality { get; set; }
    public string birth_date { get; set; }
}

public class Real_Estate_Details
{
    public string municipality { get; set; }
    public string municipality_code { get; set; }
    public string address { get; set; }
    public string cadastral_sheet { get; set; }
    public string cadastral_map_number { get; set; }
    public string cadastral_sub_number { get; set; }
    public string cadastral_category_type { get; set; }
    public string cadastral_category { get; set; }
}

public class Procedure
{
    public string charged_subject_rea_code { get; set; }
    public string procedure_type { get; set; }
    public string procedure_type_code { get; set; }
    public string deed_registration_date { get; set; }
    public string procedure_starting_date { get; set; }
    public string procedure_publication_date { get; set; }
    public string procedure_validation_date { get; set; }
    public string procedure_revocation_date { get; set; }
    public string procedure_closing_date { get; set; }
    public Trustee_Communication trustee_communication { get; set; }
    public string infocamere_grouping { get; set; }
    public string infocamere_grouping_description { get; set; }
    public Declaration declaration { get; set; }
}

public class Trustee_Communication
{
    public string court { get; set; }
    public string measure_date { get; set; }
    public string judge_name { get; set; }
    public string judge_surname { get; set; }
    public string court_hearing_date { get; set; }
    public string end_date { get; set; }
    public string place { get; set; }
    public string insertion_date { get; set; }
    public string measure_number { get; set; }
}

public class Declaration
{
    public string code { get; set; }
    public string code_type { get; set; }
    public string statement { get; set; }
}

public class Crisis_Events
{
    public Crisis_Event_Details[] crisis_event_details { get; set; }
}

public class Crisis_Event_Details
{
    public string type { get; set; }
    public int type_code { get; set; }
    public string step { get; set; }
    public string court { get; set; }
    public string resolution_description { get; set; }
    public string deliberative_body { get; set; }
    public string resolution_date { get; set; }
    public string resolution_transcription_date { get; set; }
    public string resolution_upload_date { get; set; }
    public Petition[] petitions { get; set; }
    public Judicial_Writs[] judicial_writs { get; set; }
}

public class Petition
{
    public string type { get; set; }
    public string date { get; set; }
    public string transcription_date { get; set; }
}

public class Judicial_Writs
{
    public string type { get; set; }
    public string filing_date { get; set; }
    public string transcripion_date { get; set; }
    public int granted_days { get; set; }
    public int extension_days { get; set; }
    public string judgement_date { get; set; }
    public string plan_end_date { get; set; }
}

public class Cigs_Decrees
{
    public string issue_date { get; set; }
    public string start_date { get; set; }
    public string end_date { get; set; }
    public int decree_id { get; set; }
    public string type { get; set; }
    public int sequence_number { get; set; }
    public string status_code { get; set; }
    public string status { get; set; }
    public string reason_code { get; set; }
    public string reason { get; set; }
    public string direct_payment_authorization_flag { get; set; }
    public string category_code { get; set; }
    public string category { get; set; }
    public string grouping_code { get; set; }
    public int[] local_branches_subject_ids { get; set; }
}

public class Personal_Bankruptcies
{
    public Company1[] companies { get; set; }
}

public class Company1
{
    public int related_company_subject_id { get; set; }
    public string relation_type { get; set; }
    public Bankrupt[] bankrupts { get; set; }
}

public class Bankrupt
{
    public string type { get; set; }
    public string bankruptcy_id { get; set; }
    public string bankruptcy_date { get; set; }
    public string judgment_date { get; set; }
    public string judgment_number { get; set; }
    public string trustee { get; set; }
    public string judicial_body_type_code { get; set; }
    public string judicial_body_type { get; set; }
    public string judicial_body_province { get; set; }
    public string insertion_date { get; set; }
    public string court { get; set; }
    public string reference_date { get; set; }
    public string modification_date { get; set; }
    public string extension_name { get; set; }
    public string extension_surname { get; set; }
    public string extension_tax_code { get; set; }
}

public class Person
{
    public int subject_id { get; set; }
    public string tax_code { get; set; }
    public string vat_number { get; set; }
    public string name { get; set; }
    public Protest[] protests { get; set; }
    public Prejudicial_Events[] prejudicial_events { get; set; }
    public Procedure1[] procedures { get; set; }
    public Crisis_Events1[] crisis_events { get; set; }
    public Cigs_Decrees1[] cigs_decrees { get; set; }
    public Personal_Bankruptcies1[] personal_bankruptcies { get; set; }
}

public class Charged_Subjects_Personal_Data1
{
    public string name { get; set; }
    public string tax_code { get; set; }
    public string birth_province_code { get; set; }
    public string birth_municipality { get; set; }
    public string birth_date { get; set; }
}

public class Beneficiary_Subjects_Personal_Data1
{
    public string name { get; set; }
    public string tax_code { get; set; }
    public string birth_province_code { get; set; }
    public string birth_municipality { get; set; }
    public string birth_date { get; set; }
}

public class Real_Estate_Details1
{
    public string municipality { get; set; }
    public string municipality_code { get; set; }
    public string address { get; set; }
    public string cadastral_sheet { get; set; }
    public string cadastral_map_number { get; set; }
    public string cadastral_sub_number { get; set; }
    public string cadastral_category_type { get; set; }
    public string cadastral_category { get; set; }
}

public class Procedure1
{
    public string charged_subject_rea_code { get; set; }
    public string procedure_type { get; set; }
    public string procedure_type_code { get; set; }
    public string deed_registration_date { get; set; }
    public string procedure_starting_date { get; set; }
    public string procedure_publication_date { get; set; }
    public string procedure_validation_date { get; set; }
    public string procedure_revocation_date { get; set; }
    public string procedure_closing_date { get; set; }
    public Trustee_Communication1 trustee_communication { get; set; }
    public string infocamere_grouping { get; set; }
    public string infocamere_grouping_description { get; set; }
    public Declaration1 declaration { get; set; }
}

public class Trustee_Communication1
{
    public string court { get; set; }
    public string measure_date { get; set; }
    public string judge_name { get; set; }
    public string judge_surname { get; set; }
    public string court_hearing_date { get; set; }
    public string end_date { get; set; }
    public string place { get; set; }
    public string insertion_date { get; set; }
    public string measure_number { get; set; }
}

public class Declaration1
{
    public string code { get; set; }
    public string code_type { get; set; }
    public string statement { get; set; }
}

public class Crisis_Events1
{
    public Crisis_Event_Details1[] crisis_event_details { get; set; }
}

public class Crisis_Event_Details1
{
    public string type { get; set; }
    public int type_code { get; set; }
    public string step { get; set; }
    public string court { get; set; }
    public string resolution_description { get; set; }
    public string deliberative_body { get; set; }
    public string resolution_date { get; set; }
    public string resolution_transcription_date { get; set; }
    public string resolution_upload_date { get; set; }
    public Petition1[] petitions { get; set; }
    public Judicial_Writs1[] judicial_writs { get; set; }
}

public class Petition1
{
    public string type { get; set; }
    public string date { get; set; }
    public string transcription_date { get; set; }
}

public class Judicial_Writs1
{
    public string type { get; set; }
    public string filing_date { get; set; }
    public string transcripion_date { get; set; }
    public int granted_days { get; set; }
    public int extension_days { get; set; }
    public string judgement_date { get; set; }
    public string plan_end_date { get; set; }
}

public class Cigs_Decrees1
{
    public string issue_date { get; set; }
    public string start_date { get; set; }
    public string end_date { get; set; }
    public int decree_id { get; set; }
    public string type { get; set; }
    public int sequence_number { get; set; }
    public string status_code { get; set; }
    public string status { get; set; }
    public string reason_code { get; set; }
    public string reason { get; set; }
    public string direct_payment_authorization_flag { get; set; }
    public string category_code { get; set; }
    public string category { get; set; }
    public string grouping_code { get; set; }
    public int[] local_branches_subject_ids { get; set; }
}

public class Personal_Bankruptcies1
{
    public Company2[] companies { get; set; }
}

public class Company2
{
    public int related_company_subject_id { get; set; }
    public string relation_type { get; set; }
    public Bankrupt1[] bankrupts { get; set; }
}

public class Bankrupt1
{
    public string type { get; set; }
    public string bankruptcy_id { get; set; }
    public string bankruptcy_date { get; set; }
    public string judgment_date { get; set; }
    public string judgment_number { get; set; }
    public string trustee { get; set; }
    public string judicial_body_type_code { get; set; }
    public string judicial_body_type { get; set; }
    public string judicial_body_province { get; set; }
    public string insertion_date { get; set; }
    public string court { get; set; }
    public string reference_date { get; set; }
    public string modification_date { get; set; }
    public string extension_name { get; set; }
    public string extension_surname { get; set; }
    public string extension_tax_code { get; set; }
}
