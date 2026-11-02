namespace RobotBombero.Simulation
{
    public static class Score
    {
        /// <summary>Puntos = (fuegos / pasos) x pasos², que equivale a fuegos x pasos.</summary>
        public static long Points(int fires, int steps)
        {
            return (long)fires * steps;
        }
    }
}
