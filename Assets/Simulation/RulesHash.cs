using System;
using System.Security.Cryptography;
using System.Text;

namespace RobotBombero.Simulation
{
    /// <summary>
    /// Huella SHA-256 (hexadecimal en minúsculas) de las reglas: cuenta el orden y los valores,
    /// no los nombres. Cada campo lleva prefijo de longitud para que no haya colisiones entre límites.
    /// </summary>
    public static class RulesHash
    {
        public static string Compute(Rule[] rules)
        {
            if (rules == null) throw new ArgumentNullException("rules");

            var sb = new StringBuilder();
            sb.Append(rules.Length).Append('|');
            foreach (Rule rule in rules)
            {
                if (rule == null) throw new ArgumentException("Regla nula.", "rules");
                Field(sb, rule.battery);
                Field(sb, rule.temp);
                Field(sb, rule.light);
                Field(sb, rule.prox);
                Field(sb, rule.cell);
                int count = rule.actions == null ? 0 : rule.actions.Length;
                sb.Append(count).Append('|');
                for (int i = 0; i < count; i++) Field(sb, rule.actions[i]);
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        private static void Field(StringBuilder sb, string value)
        {
            string v = value ?? "";
            sb.Append(v.Length).Append(':').Append(v).Append('|');
        }
    }
}
