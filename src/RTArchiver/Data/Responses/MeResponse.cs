using System.Text.Json.Serialization;

namespace RTArchiver.Data.Responses;

public class MeResponse
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = String.Empty;
	
	[JsonPropertyName("type")]
	public string Type { get; set; } = String.Empty;

    [JsonPropertyName("attributes")]
    public MeResponse_Attributes Attributes { get; set; }

    [JsonPropertyName("meta")]
    public MeResponse_Meta Meta { get; set; }
}

public class MeResponse_Attributes
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }
    
    [JsonPropertyName("username")]
    public string Username { get; set; } = String.Empty;
}

public class MeResponse_Meta
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("full_name")]
    public string FullName { get; set; }
}

/*

public class Attributes
{
    public string uuid { get; set; }
    public string created_at { get; set; }
    public string username { get; set; }
    public string display_title { get; set; }
    public Pictures pictures { get; set; }
    public string member_tier { get; set; }
    public int member_tier_i { get; set; }
    public bool used_trial { get; set; }
    public object expires_at { get; set; }
    public object[] social_connected { get; set; }
    public string[] badges { get; set; }
    public string about { get; set; }
    public string location { get; set; }
    public string timezone { get; set; }
    public string store_region { get; set; }
    public object banned_until { get; set; }
    public bool is_first_plus { get; set; }
    public string email { get; set; }
    public bool email_verified { get; set; }
    public int db_id { get; set; }
    public object[] roles { get; set; }
    public string birthday { get; set; }
    public string sex { get; set; }
    public string last_login_at { get; set; }
    public object deleted_at { get; set; }
    public bool opt_out_selling_data { get; set; }
    public string supporting_cast_login_url { get; set; }
    public string supporting_cast_login_token { get; set; }
    public Sponsorship_details sponsorship_details { get; set; }
    public Preferences preferences { get; set; }
    public bool beta { get; set; }
    public bool rtf1 { get; set; }
    public string ipaddress { get; set; }
}

public class Pictures
{
    public Tb tb { get; set; }
    public Sm sm { get; set; }
    public Md md { get; set; }
    public Original original { get; set; }
}

public class Tb
{
    public string profile { get; set; }
    public string cover { get; set; }
}

public class Sm
{
    public string profile { get; set; }
    public string cover { get; set; }
}

public class Md
{
    public string profile { get; set; }
    public string cover { get; set; }
}

public class Original
{
    public string profile { get; set; }
    public string cover { get; set; }
}

public class Sponsorship_details
{
    public string sponsorship_starts_at { get; set; }
    public string sponsorship_ends_at { get; set; }
    public string sponsorship_type { get; set; }
    public string double_gold_ends_at { get; set; }
    public string plan_code { get; set; }
    public bool is_trial { get; set; }
    public string subscription_started_at { get; set; }
    public object subscription_paused_at { get; set; }
    public bool used_trial { get; set; }
}

public class Preferences
{
    public bool hide_birthday { get; set; }
    public bool hide_personal_comments { get; set; }
    public bool allow_direct_messages { get; set; }
    public bool allow_group_invites { get; set; }
    public bool allow_profile_comments { get; set; }
    public bool allow_friend_requests { get; set; }
    public bool autoplay { get; set; }
    public bool miniplayer { get; set; }
    public string prefered_quality { get; set; }
    public double volume { get; set; }
    public string user_color { get; set; }
    public string user_hex_color { get; set; }
}

public class Meta
{
    public int id { get; set; }
    public string full_name { get; set; }
    public string address1 { get; set; }
    public string address2 { get; set; }
    public string city { get; set; }
    public string state_province { get; set; }
    public string country { get; set; }
    public string zip_postal { get; set; }
    public string shirt_size { get; set; }
    public string status { get; set; }
    public double shipping_cost { get; set; }
    public int created_by { get; set; }
    public int updated_by { get; set; }
    public object deleted_by { get; set; }
    public object last_bill_date { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
    public object deleted_at { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
}
*/
