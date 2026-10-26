namespace BunjangAuto
{
    public static class Brand
    {
        public const string Name = "EXP";
        public const string Master = "EXP Master";
        public const string Version = "2.3.0";
        public static string Email
        {
            get
            {
                string value = System.Environment.GetEnvironmentVariable("CONTACT_EMAIL");
                if (System.String.IsNullOrWhiteSpace(value))
                    throw new System.InvalidOperationException("CONTACT_EMAIL 환경변수에 공개 연락처를 지정해야 합니다.");
                return value;
            }
        }
        public const string Tagline = "판매를 가볍게.";
    }
}
