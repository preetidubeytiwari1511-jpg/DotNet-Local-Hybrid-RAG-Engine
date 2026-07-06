namespace SmartAiApi
{
    public static class TextChunker
    {
        public static List<string> SplitIntoChunks(string text, int chunkSize = 500, int chunkOverlap = 100)
        {
            var chunks = new List<string>();

            // Edge Case Handling: Agar text pehle se hi chunk size se chota ho
            if (string.IsNullOrEmpty(text)) return chunks;
            if (text.Length <= chunkSize)
            {
                chunks.Add(text);
                return chunks;
            }

            int index = 0;
            while (index < text.Length)
            {
                // 1. Calculate length of the current chunk
                int remainingLength = text.Length - index;
                int currentChunkLength = Math.Min(chunkSize, remainingLength);

                // 2. Text mein se chunk slice karke nikalein
                string chunk = text.Substring(index, currentChunkLength);
                chunks.Add(chunk);

                // 3. Move the pointer forward (Subtract overlap to create continuity)
                index += (chunkSize - chunkOverlap);

                // Safety Check: Agar remaining string overlap size se choti ho, to loop rok dein
                if (index >= text.Length || remainingLength <= chunkOverlap)
                {
                    break;
                }
            }

            return chunks;
        }
    }
}
