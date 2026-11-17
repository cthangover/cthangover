namespace Cthangover.TestRunner
{
    /// <summary>Content-addressed relative file evidence; restoration never rereads mutable live source bytes.</summary>
    public sealed class PreparedInputFile
    {
        /// <summary>Canonical project-relative path, without traversal or a linked source.</summary>
        public string Path
        {
            get; set;
        }

        /// <summary>Byte count verified during hashing and after a materialization.</summary>
        public long Length
        {
            get; set;
        }

        /// <summary>SHA256 of the complete file, including same-size/same-timestamp edits.</summary>
        public string Hash
        {
            get; set;
        }

        /// <summary>Directory entries preserve empty authoring/input directories without counting them as copied files.</summary>
        public bool IsDirectory
        {
            get; set;
        }
    }
}
