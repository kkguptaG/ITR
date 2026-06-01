namespace TallyG.Tax.Domain.Enums;

/// <summary>Reason an OTP challenge was issued (Ch.2 otp_tokens).</summary>
public enum OtpPurpose
{
    Signup = 0,
    Login = 1,
    ResetPassword = 2,
    EfileEvc = 3,
    SensitiveAction = 4
}
