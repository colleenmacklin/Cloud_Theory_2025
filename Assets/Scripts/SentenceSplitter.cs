using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class SentenceSplitter : MonoBehaviour
{
    // A sample paragraph to test the splitting
    [TextArea(3, 10)]
    public string paragraphText = "Mr. Jones went to the store. He bought apples, oranges, etc. Was it a good trip? Yes!";

    // This method splits the paragraph into a list of sentences
    public List<string> SplitParagraphIntoSentences(string text)
    {
        // Define a robust regex pattern to split by sentence endings (periods, question marks, exclamation marks)
        // while attempting to handle common abbreviations and new lines.
        // The pattern splits on punctuation followed by whitespace, newlines, or the end of the string.
        string pattern = @"(?<=[.!?])\s+|\n|\r";
        
        // Use Regex.Split to get an array of potential sentences
        string[] sentencesArray = Regex.Split(text, pattern);

        // Create a list to store the cleaned sentences
        List<string> sentencesList = new List<string>();

        // Iterate through the results and clean them up
        foreach (string sentence in sentencesArray)
        {
            string cleanedSentence = sentence.Trim();
            if (!string.IsNullOrEmpty(cleanedSentence))
            {
                sentencesList.Add(cleanedSentence);
            }
        }

        return sentencesList;
    }
}
