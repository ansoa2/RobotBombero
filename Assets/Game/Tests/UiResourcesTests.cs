using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace RobotBombero.Game.Tests
{
    /// <summary>
    /// Fallos que solo se ven fuera del editor. Font.HasCharacter da true en el editor gracias a las fuentes del sistema,
    /// por eso se lee la tabla de caracteres del propio archivo de la fuente.
    /// </summary>
    public class UiResourcesTests
    {
        private const string UiPath = "Assets/Game/UI";

        [Test]
        public void Fonts_ContainEveryNonAsciiCharacterOfTheInterface()
        {
            var used = new SortedSet<int>();
            foreach (string file in Directory.GetFiles(UiPath, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".uxml") && !file.EndsWith(".cs")) continue;
                foreach (char c in File.ReadAllText(file))
                    if (c > 127 && !char.IsSurrogate(c)) used.Add(c);
            }
            Assert.IsNotEmpty(used);

            foreach (string font in new[] { "Inter-Regular.ttf", "Inter-SemiBold.ttf" })
            {
                HashSet<int> glyphs = ReadCharacters(File.ReadAllBytes(UiPath + "/Fonts/" + font));
                int[] missing = used.Where(c => !glyphs.Contains(c)).ToArray();
                Assert.IsEmpty(missing, font + " no tiene: " + string.Join(" ", missing.Select(c => ((char)c).ToString())));
            }
        }

        [Test]
        public void Stylesheets_DoNotUsePseudoClassesThatUssIgnores()
        {
            foreach (string name in new[] { "Theme.uss", "Screens.uss" })
            {
                string css = File.ReadAllText(UiPath + "/" + name);
                StringAssert.DoesNotContain(":first-child", css, name);
                StringAssert.DoesNotContain(":last-child", css, name);
            }
        }

        [Test]
        public void ButtonStates_OutrankTheDefaultTheme()
        {
            // El tema por defecto usa :hover:enabled; una regla con solo :hover pierde y el botón queda gris claro con texto blanco.
            foreach (string name in new[] { "Theme.uss", "Screens.uss" })
                foreach (string line in File.ReadAllLines(UiPath + "/" + name))
                    if (line.StartsWith(".") && line.Contains(".btn") && (line.Contains(":hover") || line.Contains(":active")))
                        StringAssert.Contains(":enabled", line, name);
        }

        /// <summary>Caracteres de la tabla cmap Unicode de un TTF (formatos 4 y 12).</summary>
        private static HashSet<int> ReadCharacters(byte[] ttf)
        {
            int U16(int at) { return ttf[at] << 8 | ttf[at + 1]; }
            int I32(int at) { return ttf[at] << 24 | ttf[at + 1] << 16 | ttf[at + 2] << 8 | ttf[at + 3]; }

            int cmap = -1;
            for (int i = 0; i < U16(4); i++)
            {
                int entry = 12 + i * 16;
                if (ttf[entry] == 'c' && ttf[entry + 1] == 'm' && ttf[entry + 2] == 'a' && ttf[entry + 3] == 'p') cmap = I32(entry + 8);
            }
            if (cmap < 0) throw new InvalidDataException("La fuente no tiene tabla cmap.");

            var result = new HashSet<int>();
            for (int i = 0; i < U16(cmap + 2); i++)
            {
                int record = cmap + 4 + i * 8;
                int platform = U16(record);
                if (platform != 0 && platform != 3) continue;
                int table = cmap + I32(record + 4);
                int format = U16(table);
                if (format == 4)
                {
                    int segments = U16(table + 6) / 2;
                    int ends = table + 14, starts = ends + segments * 2 + 2, deltas = starts + segments * 2, offsets = deltas + segments * 2;
                    for (int s = 0; s < segments; s++)
                    {
                        int end = U16(ends + s * 2), start = U16(starts + s * 2), delta = U16(deltas + s * 2), offset = U16(offsets + s * 2);
                        for (int c = start; c <= end && c != 0xFFFF; c++)
                        {
                            int glyph = offset == 0 ? (c + delta) & 0xFFFF : U16(offsets + s * 2 + offset + (c - start) * 2);
                            if (glyph != 0) result.Add(c);
                        }
                    }
                }
                else if (format == 12)
                {
                    for (int g = 0; g < I32(table + 12); g++)
                    {
                        int group = table + 16 + g * 12;
                        for (int c = I32(group); c <= I32(group + 4); c++) result.Add(c);
                    }
                }
            }
            return result;
        }
    }
}
