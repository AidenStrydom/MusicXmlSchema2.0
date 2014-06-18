using System;
using System.Collections.Generic;
using System.Linq;
using MusicXml2;

namespace MusicXmlTester
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            const string input = @"../../../Test Data/cantus firmus.xml";
            const string output = @"../../../Test Data/output.xml";
            ScorePartwise.Serialize(output, ScorePartwise.Deserialize(input));

            var score = ScorePartwise.Deserialize(input);
            var list = new List<int>();

            foreach (var part in score.Parts)
            {
                Console.WriteLine("Part " + part.Id);

                list.AddRange(from measure in part.Measures 
                              from note in measure.Notes.Where(note => note.Staff == "1") 
                              select MidiMapping.MidiLookUp(note.Pitch));
            }

            foreach (var i in list)
                Console.Write(i + " ");

            Console.WriteLine("\r\n\r\npress any key to continue...");
            Console.ReadLine();
        }
    }
}