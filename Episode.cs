namespace FFMPEGEncoder
{
    public class Episode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;
        public string EncodePath { get; set; } = string.Empty;
        public bool Encoded { get; set; }

        public int SerieId { get; set; }



    }
    public class Serie
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Done { get; set; }
    }
}
