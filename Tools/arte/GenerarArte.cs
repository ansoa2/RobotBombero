// Genera los SVG de Assets/Game/Art. Las escenas son deterministas (semillas fijas).
// Uso: dotnet run Tools/arte/GenerarArte.cs -- Assets/Game/Art
// Después, Tools/arte/Renderizar.ps1 los convierte a PNG con Edge.
using System.Globalization;
using System.Text;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
string outDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(outDir);

File.WriteAllText(Path.Combine(outDir, "fondo-vertical.svg"), Arte.Escena(new Plano(
    W: 1080, H: 1920, Horizonte: 1210, FuegoX: 790, TorreW: 230, TorreTop: 560,
    RobotX: 120, RobotY: 590, RobotEscala: 1.15, AzoteaY: 1130, Semilla: 11, Vertical: true)));
File.WriteAllText(Path.Combine(outDir, "fondo-horizontal.svg"), Arte.Escena(new Plano(
    W: 1920, H: 1080, Horizonte: 985, FuegoX: 1560, TorreW: 250, TorreTop: 320,
    RobotX: 860, RobotY: 455, RobotEscala: 1.08, AzoteaY: 962, Semilla: 23, Vertical: false)));
File.WriteAllText(Path.Combine(outDir, "robot.svg"), Arte.RobotSuelto());
File.WriteAllText(Path.Combine(outDir, "fuego.svg"), Arte.FuegoSuelto());

record Plano(int W, int H, int Horizonte, int FuegoX, int TorreW, int TorreTop,
    int RobotX, int RobotY, double RobotEscala, int AzoteaY, int Semilla, bool Vertical);

static class Arte
{
    static readonly string[] Ventanas = { "#FFCF7A", "#FFB45C", "#FFE3A6", "#FF9A4A" };

    static string N(double v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);

    public static string Escena(Plano p)
    {
        var r = new Random(p.Semilla);
        var s = new StringBuilder();
        s.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{p.W}\" height=\"{p.H}\" viewBox=\"0 0 {p.W} {p.H}\">\n");
        s.Append("<defs>\n").Append(Defs(p)).Append(RobotDefs()).Append("</defs>\n");

        // Cielo de noche a brasa.
        s.Append($"<rect width=\"{p.W}\" height=\"{p.H}\" fill=\"url(#cielo)\"/>\n");
        s.Append(Estrellas(p, r));
        s.Append($"<rect width=\"{p.W}\" height=\"{p.H}\" fill=\"url(#resplandor)\"/>\n");

        // Humo que sube desde el incendio.
        s.Append(Humo(p));

        // Ciudad lejana y neblina.
        s.Append("<g filter=\"url(#desenfoque2)\">").Append(Skyline(p, r, 0.46, 0.86, "url(#ciudadLejana)", 0.10, 0.45, 26, 70)).Append("</g>\n");
        s.Append($"<rect y=\"{p.Horizonte - (p.Vertical ? 420 : 300)}\" width=\"{p.W}\" height=\"{(p.Vertical ? 460 : 320)}\" fill=\"url(#neblina)\"/>\n");

        // Plano medio.
        s.Append(Skyline(p, r, 0.34, 0.66, "url(#ciudadMedia)", 0.20, 0.7, 34, 110));

        // Torre en llamas: resplandor, edificio y fuego.
        s.Append(TorreEnLlamas(p, r));

        // Ciudad cercana, muy oscura, con el borde iluminado por el fuego.
        s.Append(Skyline(p, r, 0.14, 0.40, "url(#ciudadCercana)", 0.26, 1.0, 44, 150, excluirTorre: true));
        s.Append($"<rect y=\"{p.Horizonte - 130}\" width=\"{p.W}\" height=\"260\" fill=\"url(#neblinaBaja)\"/>\n");

        s.Append(Chispas(p, r, p.Vertical ? 150 : 170));

        // Azotea en primer plano y robot.
        s.Append(Azotea(p));
        s.Append($"<ellipse cx=\"{N(p.RobotX + 200 * p.RobotEscala)}\" cy=\"{N(p.AzoteaY + 4)}\" rx=\"{N(170 * p.RobotEscala)}\" ry=\"{N(16 * p.RobotEscala)}\" fill=\"#04050C\" opacity=\".75\"/>\n");
        s.Append($"<g transform=\"translate({p.RobotX} {p.RobotY}) scale({N(p.RobotEscala)})\" filter=\"url(#contraluz)\">").Append(Robot()).Append("</g>\n");
        s.Append($"<g transform=\"translate({p.RobotX} {p.RobotY}) scale({N(p.RobotEscala)})\"><rect x=\"136\" y=\"138\" width=\"128\" height=\"40\" rx=\"20\" fill=\"#6BE8FF\" opacity=\".38\" filter=\"url(#rbBrillo)\"/><rect x=\"136\" y=\"138\" width=\"128\" height=\"40\" rx=\"20\" fill=\"url(#rbVisor)\" opacity=\".55\"/></g>\n");

        // Chispas cercanas, grandes y desenfocadas.
        s.Append(Bokeh(p, r));

        // Calma para el menú: abajo en vertical, a la izquierda en horizontal.
        s.Append($"<rect width=\"{p.W}\" height=\"{p.H}\" fill=\"url(#calma)\"/>\n");
        s.Append($"<rect width=\"{p.W}\" height=\"{p.H}\" fill=\"url(#vineta)\"/>\n");
        s.Append("</svg>\n");
        return s.ToString();
    }

    static string Defs(Plano p)
    {
        var d = new StringBuilder();
        // El gradiente acaba en el horizonte; debajo solo queda la azotea.
        d.Append($@"<linearGradient id=""cielo"" gradientUnits=""userSpaceOnUse"" x1=""0"" y1=""0"" x2=""0"" y2=""{p.Horizonte}"">
<stop offset=""0"" stop-color=""#060914""/><stop offset="".26"" stop-color=""#0E1534""/><stop offset="".48"" stop-color=""#2A1646""/>
<stop offset="".66"" stop-color=""#6E2140""/><stop offset="".82"" stop-color=""#C9432B""/><stop offset=""1"" stop-color=""#FF9A42""/></linearGradient>
<radialGradient id=""resplandor"" gradientUnits=""userSpaceOnUse"" cx=""{p.FuegoX}"" cy=""{p.TorreTop + 30}"" r=""{(p.Vertical ? 760 : 820)}"">
<stop offset=""0"" stop-color=""#FFB347"" stop-opacity="".85""/><stop offset="".22"" stop-color=""#FF6A21"" stop-opacity="".55""/>
<stop offset="".55"" stop-color=""#C4302B"" stop-opacity="".22""/><stop offset=""1"" stop-color=""#7A1E3A"" stop-opacity=""0""/></radialGradient>
<radialGradient id=""ciudadLejana"" gradientUnits=""userSpaceOnUse"" cx=""{p.FuegoX}"" cy=""{p.Horizonte - 200}"" r=""900"">
<stop offset=""0"" stop-color=""#8E3346""/><stop offset="".45"" stop-color=""#4A2150""/><stop offset=""1"" stop-color=""#231A48""/></radialGradient>
<radialGradient id=""ciudadMedia"" gradientUnits=""userSpaceOnUse"" cx=""{p.FuegoX}"" cy=""{p.Horizonte - 200}"" r=""800"">
<stop offset=""0"" stop-color=""#5A1D2E""/><stop offset="".5"" stop-color=""#2A1538""/><stop offset=""1"" stop-color=""#141230""/></radialGradient>
<radialGradient id=""ciudadCercana"" gradientUnits=""userSpaceOnUse"" cx=""{p.FuegoX}"" cy=""{p.Horizonte - 200}"" r=""760"">
<stop offset=""0"" stop-color=""#2A1020""/><stop offset="".5"" stop-color=""#120E24""/><stop offset=""1"" stop-color=""#0A0C1C""/></radialGradient>
<linearGradient id=""torre"" x1=""0"" y1=""0"" x2=""1"" y2=""0""><stop offset=""0"" stop-color=""#1A1026""/><stop offset="".55"" stop-color=""#231026""/><stop offset=""1"" stop-color=""#3A1420""/></linearGradient>
<linearGradient id=""neblina"" x1=""0"" y1=""0"" x2=""0"" y2=""1""><stop offset=""0"" stop-color=""#FF7A2F"" stop-opacity=""0""/><stop offset="".7"" stop-color=""#FF7A2F"" stop-opacity="".30""/><stop offset=""1"" stop-color=""#FFB266"" stop-opacity="".42""/></linearGradient>
<linearGradient id=""neblinaBaja"" x1=""0"" y1=""0"" x2=""0"" y2=""1""><stop offset=""0"" stop-color=""#FF7A2F"" stop-opacity=""0""/><stop offset="".6"" stop-color=""#E5512A"" stop-opacity="".30""/><stop offset=""1"" stop-color=""#090C16"" stop-opacity="".2""/></linearGradient>
<linearGradient id=""borde"" x1=""0"" y1=""0"" x2=""0"" y2=""1""><stop offset=""0"" stop-color=""#FF8A3C""/><stop offset=""1"" stop-color=""#FF8A3C"" stop-opacity=""0""/></linearGradient>
<linearGradient id=""azotea"" gradientUnits=""userSpaceOnUse"" x1=""0"" y1=""{p.AzoteaY}"" x2=""0"" y2=""{p.AzoteaY + 240}""><stop offset=""0"" stop-color=""#1C1426""/><stop offset=""1"" stop-color=""#090C16""/></linearGradient>
<radialGradient id=""luzAzotea"" gradientUnits=""userSpaceOnUse"" cx=""{p.FuegoX}"" cy=""{p.AzoteaY}"" r=""{(p.Vertical ? 700 : 780)}""><stop offset=""0"" stop-color=""#FF8A3C""/><stop offset="".6"" stop-color=""#B8452A""/><stop offset=""1"" stop-color=""#3A1A22""/></radialGradient>
");
        if (p.Vertical)
        {
            d.Append($@"<linearGradient id=""calma"" gradientUnits=""userSpaceOnUse"" x1=""0"" y1=""{p.AzoteaY + 12}"" x2=""0"" y2=""{p.AzoteaY + 300}"">
<stop offset=""0"" stop-color=""#090C16"" stop-opacity=""0""/><stop offset="".5"" stop-color=""#090C16"" stop-opacity="".78""/><stop offset=""1"" stop-color=""#090C16""/></linearGradient>
<linearGradient id=""vineta"" x1=""0"" y1=""0"" x2=""0"" y2=""1""><stop offset=""0"" stop-color=""#060914"" stop-opacity="".62""/><stop offset="".2"" stop-color=""#060914"" stop-opacity=""0""/></linearGradient>
");
        }
        else
        {
            d.Append(@"<linearGradient id=""calma"" x1=""0"" y1=""0"" x2=""1"" y2=""0"">
<stop offset=""0"" stop-color=""#090C16""/><stop offset="".3"" stop-color=""#090C16"" stop-opacity="".96""/><stop offset="".44"" stop-color=""#090C16"" stop-opacity="".55""/><stop offset="".56"" stop-color=""#090C16"" stop-opacity=""0""/></linearGradient>
<radialGradient id=""vineta"" cx="".62"" cy="".5"" r="".75""><stop offset="".62"" stop-color=""#060914"" stop-opacity=""0""/><stop offset=""1"" stop-color=""#060914"" stop-opacity="".65""/></radialGradient>
");
        }
        d.Append(@"<filter id=""desenfoque2"" x=""-5%"" y=""-5%"" width=""110%"" height=""110%""><feGaussianBlur stdDeviation=""1.6""/></filter>
<filter id=""llama"" x=""-30%"" y=""-30%"" width=""160%"" height=""160%"" color-interpolation-filters=""sRGB"">
<feTurbulence type=""fractalNoise"" baseFrequency="".008 .017"" numOctaves=""2"" seed=""7"" result=""n""/>
<feDisplacementMap in=""SourceGraphic"" in2=""n"" scale=""46"" xChannelSelector=""R"" yChannelSelector=""G"" result=""d""/>
<feGaussianBlur in=""d"" stdDeviation=""1.1"" result=""s""/><feGaussianBlur in=""d"" stdDeviation=""13"" result=""g""/>
<feBlend in=""s"" in2=""g"" mode=""screen""/></filter>
<filter id=""llamaChica"" x=""-30%"" y=""-30%"" width=""160%"" height=""160%"" color-interpolation-filters=""sRGB"">
<feTurbulence type=""fractalNoise"" baseFrequency="".014 .026"" numOctaves=""2"" seed=""3"" result=""n""/>
<feDisplacementMap in=""SourceGraphic"" in2=""n"" scale=""22"" xChannelSelector=""R"" yChannelSelector=""G""/><feGaussianBlur stdDeviation="".7""/></filter>
<filter id=""aura"" x=""-60%"" y=""-60%"" width=""220%"" height=""220%""><feGaussianBlur stdDeviation=""46""/></filter>
<filter id=""auraChica"" x=""-80%"" y=""-80%"" width=""260%"" height=""260%""><feGaussianBlur stdDeviation=""12""/></filter>
<filter id=""chispa"" x=""-10%"" y=""-10%"" width=""120%"" height=""120%""><feGaussianBlur stdDeviation=""1.1"" result=""b""/><feMerge><feMergeNode in=""b""/><feMergeNode in=""b""/><feMergeNode in=""SourceGraphic""/></feMerge></filter>
<filter id=""bokeh"" x=""-20%"" y=""-20%"" width=""140%"" height=""140%""><feGaussianBlur stdDeviation=""5""/></filter>
<filter id=""humo"" x=""-40%"" y=""-40%"" width=""180%"" height=""180%"" color-interpolation-filters=""sRGB"">
<feTurbulence type=""fractalNoise"" baseFrequency="".0055 .0085"" numOctaves=""5"" seed=""4"" result=""n""/>
<feDisplacementMap in=""SourceGraphic"" in2=""n"" scale=""260"" xChannelSelector=""R"" yChannelSelector=""G"" result=""d""/>
<feGaussianBlur in=""d"" stdDeviation=""16"" result=""b""/>
<feColorMatrix in=""n"" type=""matrix"" values=""0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  2.7 0 0 0 -.55"" result=""a""/>
<feComposite in=""b"" in2=""a"" operator=""in""/></filter>
<filter id=""contraluz"" x=""-15%"" y=""-10%"" width=""130%"" height=""120%"" color-interpolation-filters=""sRGB"">
<feOffset in=""SourceAlpha"" dx=""-9"" dy=""4"" result=""o""/>
<feComposite in=""SourceAlpha"" in2=""o"" operator=""out"" result=""borde""/>
<feGaussianBlur in=""borde"" stdDeviation=""2.2"" result=""bb""/>
<feFlood flood-color=""#FF9A4A""/><feComposite in2=""bb"" operator=""in"" result=""luz""/>
<feComposite in=""luz"" in2=""SourceAlpha"" operator=""in"" result=""rim""/>
<feComponentTransfer in=""SourceGraphic"" result=""sombra""><feFuncR type=""linear"" slope="".74""/><feFuncG type=""linear"" slope="".66""/><feFuncB type=""linear"" slope="".72""/></feComponentTransfer>
<feMerge><feMergeNode in=""sombra""/><feMergeNode in=""rim""/></feMerge></filter>
");
        return d.ToString();
    }

    static string Estrellas(Plano p, Random r)
    {
        var s = new StringBuilder("<g fill=\"#E6ECFF\">");
        int alto = (int)(p.Horizonte * (p.Vertical ? .36 : .46));
        for (int i = 0; i < 170; i++)
        {
            double x = r.NextDouble() * p.W, y = r.NextDouble() * alto;
            double t = r.NextDouble();
            s.Append($"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"{N(.6 + t * t * 1.5)}\" opacity=\"{N(.2 + t * .7)}\"/>");
        }
        return s.Append("</g>\n").ToString();
    }

    static string Humo(Plano p)
    {
        double fx = p.FuegoX, top = p.TorreTop;
        double des = p.Vertical ? -180 : -300;
        string columna(double dx, double w, double alto) =>
            $"M{N(fx - w * .45 + dx * .1)} {N(top + 40)} C{N(fx - w + dx * .3)} {N(top - alto * .35)} {N(fx + dx * .5 - w * 1.2)} {N(top - alto * .7)} {N(fx + dx - w * 1.7)} {N(top - alto)} " +
            $"L{N(fx + dx + w * 1.7)} {N(top - alto)} C{N(fx + dx * .5 + w * 1.2)} {N(top - alto * .7)} {N(fx + dx * .1 + w)} {N(top - alto * .35)} {N(fx + w * .45)} {N(top + 40)}Z";
        var s = new StringBuilder();
        s.Append($"<path d=\"{columna(des, 200, p.Vertical ? 800 : 500)}\" fill=\"#160E1C\" opacity=\"1\" filter=\"url(#humo)\"/>\n");
        s.Append($"<path d=\"{columna(des * .7, 130, p.Vertical ? 620 : 440)}\" fill=\"#4A1E2E\" opacity=\".9\" filter=\"url(#humo)\"/>\n");
        s.Append($"<path d=\"{columna(des * .25, 90, p.Vertical ? 330 : 280)}\" fill=\"#B24A2A\" opacity=\".65\" filter=\"url(#humo)\"/>\n");
        return s.ToString();
    }

    /// <summary>Una franja de edificios. minH y maxH son fracciones de la altura sobre el horizonte.</summary>
    static string Skyline(Plano p, Random r, double minH, double maxH, string fill, double pVentana, double brillo, int minW, int maxW, bool excluirTorre = false)
    {
        var s = new StringBuilder("<g>\n");
        double alto = p.Horizonte * (p.Vertical ? .62 : .82);
        double x = -20;
        double escala = maxW / 110.0;
        while (x < p.W + 20)
        {
            double w = minW * 1.6 + r.NextDouble() * (maxW - minW);
            double h = alto * (minH + r.NextDouble() * (maxH - minH));
            double top = p.Horizonte - h;
            bool tapaTorre = excluirTorre && x + w > p.FuegoX - p.TorreW * .46 && x < p.FuegoX + p.TorreW * .46;
            if (tapaTorre)
            {
                // Bajo la ciudad cercana delante de la torre, para no tapar el incendio.
                top = Math.Max(top, p.Horizonte - alto * .13);
            }
            s.Append(Edificio(p, r, x, w, top, fill, pVentana, brillo, escala));
            x += w - (r.NextDouble() < .25 ? 6 : 0) + (r.NextDouble() < .18 ? r.NextDouble() * 14 : 0);
        }
        return s.Append("</g>\n").ToString();
    }

    static string Edificio(Plano p, Random r, double x, double w, double top, string fill, double pVentana, double brillo, double escala)
    {
        var s = new StringBuilder();
        double h = p.Horizonte + 300 - top;
        s.Append($"<rect x=\"{N(x)}\" y=\"{N(top)}\" width=\"{N(w)}\" height=\"{N(h)}\" fill=\"{fill}\"/>");

        // Remates de la azotea.
        int tipo = r.Next(6);
        if (tipo == 1)
        {
            double ax = x + w * (.3 + r.NextDouble() * .4), ah = 40 + r.NextDouble() * 70 * escala;
            s.Append($"<rect x=\"{N(ax)}\" y=\"{N(top - ah)}\" width=\"3\" height=\"{N(ah)}\" fill=\"{fill}\"/><rect x=\"{N(ax - 9)}\" y=\"{N(top - ah * .55)}\" width=\"21\" height=\"2.5\" fill=\"{fill}\"/>");
            if (brillo > .5) s.Append($"<circle cx=\"{N(ax + 1.5)}\" cy=\"{N(top - ah)}\" r=\"3\" fill=\"#FF4A3A\"/>");
        }
        else if (tipo == 2 && w > 50)
        {
            double tx = x + w * .2, tw = 26 * escala + 8, th = 30 * escala + 6;
            s.Append($"<rect x=\"{N(tx)}\" y=\"{N(top - th - 14)}\" width=\"{N(tw)}\" height=\"{N(th)}\" rx=\"3\" fill=\"{fill}\"/><path d=\"M{N(tx - 3)} {N(top - th - 14)}L{N(tx + tw / 2)} {N(top - th - 28)}L{N(tx + tw + 3)} {N(top - th - 14)}Z\" fill=\"{fill}\"/><rect x=\"{N(tx + 3)}\" y=\"{N(top - 14)}\" width=\"3\" height=\"14\" fill=\"{fill}\"/><rect x=\"{N(tx + tw - 6)}\" y=\"{N(top - 14)}\" width=\"3\" height=\"14\" fill=\"{fill}\"/>");
        }
        else if (tipo == 3)
        {
            double sw = w * (.45 + r.NextDouble() * .25), sh = 24 + r.NextDouble() * 60 * escala;
            s.Append($"<rect x=\"{N(x + (w - sw) / 2)}\" y=\"{N(top - sh)}\" width=\"{N(sw)}\" height=\"{N(sh)}\" fill=\"{fill}\"/>");
        }
        else if (tipo == 4)
        {
            s.Append($"<rect x=\"{N(x + w * .12)}\" y=\"{N(top - 12)}\" width=\"{N(w * .22)}\" height=\"12\" fill=\"{fill}\"/><rect x=\"{N(x + w * .55)}\" y=\"{N(top - 18)}\" width=\"{N(w * .16)}\" height=\"18\" fill=\"{fill}\"/>");
        }

        // Ventanas.
        double cw = 4 + 5 * escala, ch = 7 + 7 * escala, gx = cw * 1.9, gy = ch * 1.7;
        s.Append(Ventanitas(r, x + cw * .7, top + ch, w - cw * 1.2, p.Horizonte + 60 - top, cw, ch, gx, gy, pVentana, brillo));

        // Luz del incendio en el lado que mira al fuego.
        double centro = x + w / 2;
        double dist = Math.Abs(centro - p.FuegoX);
        if (dist > 40 && dist < (p.Vertical ? 800 : 900))
        {
            double fuerza = (1 - dist / (p.Vertical ? 800 : 900)) * .75;
            double lado = centro < p.FuegoX ? x + w - 4 : x;
            s.Append($"<rect x=\"{N(lado)}\" y=\"{N(top)}\" width=\"4\" height=\"{N(Math.Min(h, 340))}\" fill=\"url(#borde)\" opacity=\"{N(fuerza)}\"/>");
            s.Append($"<rect x=\"{N(x)}\" y=\"{N(top)}\" width=\"{N(w)}\" height=\"2.5\" fill=\"#FF8A3C\" opacity=\"{N(fuerza * .8)}\"/>");
        }
        s.Append('\n');
        return s.ToString();
    }

    static string Ventanitas(Random r, double x, double y, double w, double h, double cw, double ch, double gx, double gy, double prob, double brillo)
    {
        var s = new StringBuilder();
        for (double fy = y; fy < y + h; fy += gy)
        {
            // Plantas enteras apagadas, como en una ciudad de verdad.
            bool planta = r.NextDouble() < .75;
            for (double fx = x; fx + cw < x + w; fx += gx)
            {
                if (planta && r.NextDouble() < prob)
                {
                    string c = Ventanas[r.Next(Ventanas.Length)];
                    s.Append($"<rect x=\"{N(fx)}\" y=\"{N(fy)}\" width=\"{N(cw)}\" height=\"{N(ch)}\" fill=\"{c}\" opacity=\"{N(brillo * (.45 + r.NextDouble() * .55))}\"/>");
                }
            }
        }
        return s.ToString();
    }

    static string TorreEnLlamas(Plano p, Random r)
    {
        var s = new StringBuilder();
        double w = p.TorreW, x = p.FuegoX - w / 2, top = p.TorreTop;
        double h = p.Horizonte + 300 - top;

        // Aura detrás de la torre.
        s.Append($"<ellipse cx=\"{p.FuegoX}\" cy=\"{N(top - 30)}\" rx=\"{N(w * 1.1)}\" ry=\"{N(w * 1.25)}\" fill=\"#FF6A21\" opacity=\".6\" filter=\"url(#aura)\"/>\n");

        // Llamas de detrás, muy altas.
        s.Append(Fuego(new Random(p.Semilla + 1), p.FuegoX, top + 30, w * 1.08, p.Vertical ? 440 : 400, 9, "fuegoFondo"));

        // Edificio con ventanas abrasadas.
        s.Append($"<rect x=\"{N(x)}\" y=\"{N(top)}\" width=\"{N(w)}\" height=\"{N(h)}\" fill=\"url(#torre)\"/>\n");
        double cw = 15, ch = 23, gx = 29, gy = 41;
        int fila = 0;
        for (double fy = top + 26; fy < p.Horizonte; fy += gy, fila++)
        {
            for (double fx = x + 18; fx + cw < x + w - 10; fx += gx)
            {
                double calor = Math.Max(0, 1 - fila / 13.0);
                double t = r.NextDouble();
                string c = t < calor ? "#FF8F2E" : t < calor + .28 ? "#6A241A" : "#3A1E24";
                double o = t < calor ? .95 : .9;
                s.Append($"<rect x=\"{N(fx)}\" y=\"{N(fy)}\" width=\"{cw}\" height=\"{ch}\" fill=\"{c}\" opacity=\"{N(o)}\"/>");
            }
        }
        s.Append('\n');
        // Marca del fuego ennegreciendo la fachada.
        s.Append($"<rect x=\"{N(x + w - 6)}\" y=\"{N(top)}\" width=\"6\" height=\"{N(h)}\" fill=\"url(#borde)\" opacity=\".7\"/>\n");

        // Llamas delante de la azotea y saliendo por ventanas.
        s.Append(Fuego(new Random(p.Semilla + 2), p.FuegoX, top + 70, w * .96, p.Vertical ? 250 : 220, 7, "fuegoFrente"));
        var rv = new Random(p.Semilla + 3);
        for (int i = 0; i < 3; i++)
        {
            double fx = x + 18 + gx * rv.Next(0, (int)((w - 28) / gx)) + cw / 2;
            double fy = top + 26 + gy * (1 + rv.Next(0, 6)) + ch;
            s.Append(Fuego(new Random(p.Semilla + 10 + i), fx, fy, 44, 62 + rv.NextDouble() * 40, 3, "fuegoV" + i, aura: false));
        }
        // Otro foco en un edificio vecino, más pequeño.
        double vecinoX = p.FuegoX + p.TorreW / 2.0 + (p.Vertical ? 95 : 130);
        double vecinoY = p.TorreTop + (p.Vertical ? 300 : 270);
        s.Append($"<rect x=\"{N(vecinoX - 70)}\" y=\"{N(vecinoY)}\" width=\"140\" height=\"{N(p.Horizonte + 300 - vecinoY)}\" fill=\"#1A0F22\"/>\n");
        s.Append(Ventanitas(r, vecinoX - 60, vecinoY + 18, 124, p.Horizonte - vecinoY, 16, 22, 28, 40, .35, .9));
        s.Append(Fuego(new Random(p.Semilla + 30), vecinoX, vecinoY + 18, 138, 170, 5, "fuegoVecino"));
        return s.ToString();
    }

    static string Lengua(double cx, double b, double w, double h, double lean, string fill)
    {
        return $"<path d=\"M{N(cx - w / 2)} {N(b)}" +
            $"C{N(cx - w * .66)} {N(b - h * .28)} {N(cx - w * .30)} {N(b - h * .44)} {N(cx - w * .30 + lean * .3)} {N(b - h * .62)}" +
            $"C{N(cx - w * .20 + lean * .6)} {N(b - h * .78)} {N(cx - w * .04 + lean * .8)} {N(b - h * .86)} {N(cx + lean)} {N(b - h)}" +
            $"C{N(cx + w * .10 + lean * .7)} {N(b - h * .78)} {N(cx + w * .34 + lean * .2)} {N(b - h * .60)} {N(cx + w * .38)} {N(b - h * .40)}" +
            $"C{N(cx + w * .52)} {N(b - h * .24)} {N(cx + w * .50)} {N(b - h * .06)} {N(cx + w / 2)} {N(b)}Z\" fill=\"{fill}\"/>";
    }

    static readonly (string color, double esc)[] Capas =
    {
        ("#B8200F", 1.0), ("#E8421A", .86), ("#FF7A1F", .70), ("#FFB02E", .52), ("#FFE27A", .32), ("#FFF6D6", .15)
    };

    /// <summary>Fuego de varias lenguas. Cada capa se dibuja por encima, de rojo a casi blanco.</summary>
    static string Fuego(Random r, double cx, double b, double ancho, double alto, int lenguas, string id, bool aura = true)
    {
        var geometria = new (double x, double w, double h, double lean)[lenguas];
        for (int i = 0; i < lenguas; i++)
        {
            double u = lenguas == 1 ? .5 : i / (double)(lenguas - 1);
            double campana = Math.Sin(Math.PI * (.08 + u * .84));
            double h = alto * (.45 + .55 * campana) * (.72 + r.NextDouble() * .42);
            double w = ancho / lenguas * (2.0 + r.NextDouble() * .9);
            geometria[i] = (cx + (u - .5) * ancho, w, h, (r.NextDouble() - .5) * ancho * .16 - alto * .04);
        }
        double fundido = aura ? Math.Min(alto * .34, 70) : alto * .4;
        var s = new StringBuilder();
        s.Append($"<linearGradient id=\"g{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"0\" y1=\"{N(b - fundido)}\" x2=\"0\" y2=\"{N(b + 6)}\"><stop offset=\"0\" stop-color=\"#FFF\"/><stop offset=\"1\" stop-color=\"#000\"/></linearGradient>");
        s.Append($"<mask id=\"m{id}\" maskUnits=\"userSpaceOnUse\" x=\"{N(cx - ancho * 2)}\" y=\"{N(b - alto * 2)}\" width=\"{N(ancho * 4)}\" height=\"{N(alto * 2 + 20)}\"><rect x=\"{N(cx - ancho * 2)}\" y=\"{N(b - alto * 2)}\" width=\"{N(ancho * 4)}\" height=\"{N(alto * 2 + 20)}\" fill=\"url(#g{id})\"/></mask>");
        s.Append($"<g mask=\"url(#m{id})\"><g id=\"{id}\" filter=\"url(#{(aura ? "llama" : "llamaChica")})\">");
        foreach (var capa in Capas)
        {
            foreach (var g in geometria)
            {
                double f = capa.esc;
                s.Append(Lengua(g.x, b, g.w * (.45 + f * .55), g.h * f, g.lean * f, capa.color));
            }
        }
        s.Append("</g></g>\n");
        if (!aura) return s.ToString();
        return $"<ellipse cx=\"{N(cx)}\" cy=\"{N(b - alto * .45)}\" rx=\"{N(ancho * .72)}\" ry=\"{N(alto * .72)}\" fill=\"#FF7A2F\" opacity=\".55\" filter=\"url(#aura)\"/>\n" + s;
    }

    static string Chispas(Plano p, Random r, int cuantas)
    {
        var s = new StringBuilder("<g filter=\"url(#chispa)\">");
        for (int i = 0; i < cuantas; i++)
        {
            // Nacen en el fuego y suben abriéndose y arrastradas por el viento hacia la izquierda.
            double t = r.NextDouble();
            double y = p.TorreTop + 240 - t * t * (p.TorreTop + 200);
            double abre = 90 + (1 - y / p.Horizonte) * 480;
            double x = p.FuegoX - (p.TorreTop + 240 - y) * .32 + (r.NextDouble() - .5) * abre * 1.3;
            double z = r.NextDouble();
            double rad = .9 + z * z * 3.0;
            string c = z > .85 ? "#FFD27A" : z > .4 ? "#FFA63D" : "#FF6F2A";
            double o = .45 + r.NextDouble() * .55;
            if (z > .78)
            {
                double dx = -3 - r.NextDouble() * 6, dy = -3 - r.NextDouble() * 7;
                s.Append($"<line x1=\"{N(x)}\" y1=\"{N(y)}\" x2=\"{N(x + dx)}\" y2=\"{N(y + dy)}\" stroke=\"{c}\" stroke-width=\"{N(rad * .7)}\" stroke-linecap=\"round\" opacity=\"{N(o)}\"/>");
            }
            else s.Append($"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"{N(rad)}\" fill=\"{c}\" opacity=\"{N(o)}\"/>");
        }
        return s.Append("</g>\n").ToString();
    }

    static string Bokeh(Plano p, Random r)
    {
        var s = new StringBuilder("<g filter=\"url(#bokeh)\">");
        for (int i = 0; i < 12; i++)
        {
            double x = p.Vertical ? 60 + r.NextDouble() * (p.W - 120) : p.W * .56 + r.NextDouble() * p.W * .42;
            double y = 120 + r.NextDouble() * (p.AzoteaY - 200);
            s.Append($"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"{N(4 + r.NextDouble() * 7)}\" fill=\"#FFB347\" opacity=\"{N(.2 + r.NextDouble() * .4)}\"/>");
        }
        return s.Append("</g>\n").ToString();
    }

    static string Azotea(Plano p)
    {
        int y = p.AzoteaY;
        var s = new StringBuilder();
        // Barandilla por detrás del robot.
        s.Append("<g fill=\"#120E1E\">");
        for (int x = 20; x < p.W; x += 84)
            s.Append($"<rect x=\"{x}\" y=\"{y - 62}\" width=\"6\" height=\"62\"/>");
        s.Append($"<rect y=\"{y - 66}\" width=\"{p.W}\" height=\"7\"/><rect y=\"{y - 34}\" width=\"{p.W}\" height=\"4\"/></g>\n");
        s.Append($"<rect y=\"{y - 66}\" width=\"{p.W}\" height=\"2\" fill=\"url(#luzAzotea)\" opacity=\".85\"/>\n");
        // Conductos y caja de ventilación en la zona del fuego.
        double cx = p.Vertical ? p.W - 190 : p.W - 330;
        s.Append($"<g fill=\"#15101F\"><rect x=\"{N(cx)}\" y=\"{y - 92}\" width=\"150\" height=\"92\" rx=\"5\"/><rect x=\"{N(cx + 22)}\" y=\"{y - 128}\" width=\"26\" height=\"38\"/><rect x=\"{N(cx + 12)}\" y=\"{y - 138}\" width=\"46\" height=\"12\" rx=\"3\"/></g>");
        s.Append($"<rect x=\"{N(cx)}\" y=\"{y - 92}\" width=\"150\" height=\"2.5\" fill=\"#FF8A3C\" opacity=\".7\"/>");
        for (int i = 0; i < 5; i++)
            s.Append($"<rect x=\"{N(cx + 18)}\" y=\"{y - 76 + i * 14}\" width=\"114\" height=\"5\" fill=\"#0A0813\"/>");
        s.Append('\n');
        // Suelo de la azotea.
        s.Append($"<rect y=\"{y}\" width=\"{p.W}\" height=\"{p.H - y}\" fill=\"url(#azotea)\"/>\n");
        s.Append($"<rect y=\"{y}\" width=\"{p.W}\" height=\"3.5\" fill=\"url(#luzAzotea)\"/>\n");
        return s.ToString();
    }

    static string RobotDefs() => @"<linearGradient id=""rbCuerpo"" x1=""0"" y1=""0"" x2=""1"" y2=""1""><stop offset=""0"" stop-color=""#F2452F""/><stop offset="".55"" stop-color=""#D32A1F""/><stop offset=""1"" stop-color=""#8E1414""/></linearGradient>
<linearGradient id=""rbMetal"" x1=""0"" y1=""0"" x2=""1"" y2=""1""><stop offset=""0"" stop-color=""#F2F4F7""/><stop offset="".5"" stop-color=""#BCC3CD""/><stop offset=""1"" stop-color=""#6E7683""/></linearGradient>
<linearGradient id=""rbOscuro"" x1=""0"" y1=""0"" x2=""0"" y2=""1""><stop offset=""0"" stop-color=""#3C424C""/><stop offset=""1"" stop-color=""#1B1E25""/></linearGradient>
<linearGradient id=""rbCasco"" x1=""0"" y1=""0"" x2=""1"" y2=""1""><stop offset=""0"" stop-color=""#FFE670""/><stop offset="".5"" stop-color=""#FFC91F""/><stop offset=""1"" stop-color=""#C98A0C""/></linearGradient>
<linearGradient id=""rbAla"" x1=""0"" y1=""0"" x2=""0"" y2=""1""><stop offset=""0"" stop-color=""#FFD23F""/><stop offset=""1"" stop-color=""#B47A08""/></linearGradient>
<linearGradient id=""rbVisor"" x1=""0"" y1=""0"" x2=""1"" y2=""0""><stop offset=""0"" stop-color=""#38CFF2""/><stop offset="".5"" stop-color=""#9FF4FF""/><stop offset=""1"" stop-color=""#1FA8D6""/></linearGradient>
<radialGradient id=""rbNucleo""><stop offset=""0"" stop-color=""#E8FDFF""/><stop offset="".5"" stop-color=""#6BE8FF""/><stop offset=""1"" stop-color=""#0E5C78""/></radialGradient>
<filter id=""rbBrillo"" x=""-40%"" y=""-80%"" width=""180%"" height=""260%""><feGaussianBlur stdDeviation=""9""/></filter>
";

    /// <summary>El robot en un lienzo de 400 x 480, con los pies en y=470.</summary>
    static string Robot() => @"
<path d=""M312 300C352 322 368 372 340 408C322 432 288 436 268 452"" fill=""none"" stroke=""#20242C"" stroke-width=""13"" stroke-linecap=""round""/>
<rect x=""128"" y=""372"" width=""56"" height=""72"" rx=""12"" fill=""url(#rbOscuro)""/><rect x=""216"" y=""372"" width=""56"" height=""72"" rx=""12"" fill=""url(#rbOscuro)""/>
<path d=""M112 470V448C112 430 126 422 142 422H172C188 422 198 432 198 448V470Z"" fill=""url(#rbOscuro)""/><path d=""M202 470V448C202 432 212 422 228 422H258C274 422 288 430 288 448V470Z"" fill=""url(#rbOscuro)""/>
<rect x=""112"" y=""452"" width=""86"" height=""9"" fill=""#FFD23F""/><rect x=""202"" y=""452"" width=""86"" height=""9"" fill=""#FFD23F""/>
<rect x=""46"" y=""226"" width=""50"" height=""108"" rx=""24"" fill=""url(#rbMetal)""/><rect x=""52"" y=""318"" width=""40"" height=""44"" rx=""16"" fill=""url(#rbOscuro)""/>
<rect x=""96"" y=""216"" width=""208"" height=""172"" rx=""44"" fill=""url(#rbCuerpo)""/>
<rect x=""96"" y=""316"" width=""208"" height=""34"" fill=""#FFD23F""/><rect x=""96"" y=""326"" width=""208"" height=""13"" fill=""#E9EDF2""/>
<rect x=""148"" y=""236"" width=""104"" height=""64"" rx=""16"" fill=""#2A0C10"" opacity=""0.72""/>
<circle cx=""200"" cy=""268"" r=""22"" fill=""#6BE8FF"" opacity=""0.5"" filter=""url(#rbBrillo)""/><circle cx=""200"" cy=""268"" r=""17"" fill=""url(#rbNucleo)""/>
<circle cx=""166"" cy=""254"" r=""4.5"" fill=""#FFD23F""/><circle cx=""166"" cy=""270"" r=""4.5"" fill=""#FF7A2F""/><circle cx=""166"" cy=""286"" r=""4.5"" fill=""#6BE8FF""/>
<rect x=""226"" y=""252"" width=""16"" height=""4"" rx=""2"" fill=""#E9EDF2""/><rect x=""226"" y=""264"" width=""16"" height=""4"" rx=""2"" fill=""#E9EDF2""/><rect x=""226"" y=""276"" width=""16"" height=""4"" rx=""2"" fill=""#E9EDF2""/>
<circle cx=""74"" cy=""232"" r=""34"" fill=""url(#rbCuerpo)""/><circle cx=""326"" cy=""232"" r=""34"" fill=""url(#rbCuerpo)""/>
<rect x=""304"" y=""228"" width=""46"" height=""84"" rx=""22"" fill=""url(#rbMetal)""/>
<path d=""M326 300C346 306 358 316 366 306C372 298 362 284 344 276"" fill=""url(#rbMetal)""/>
<rect x=""300"" y=""282"" width=""58"" height=""42"" rx=""15"" fill=""url(#rbOscuro)""/>
<rect x=""340"" y=""270"" width=""54"" height=""26"" rx=""6"" fill=""#C9CED6""/><rect x=""384"" y=""262"" width=""14"" height=""42"" rx=""4"" fill=""#FFD23F""/>
<rect x=""164"" y=""196"" width=""72"" height=""34"" rx=""12"" fill=""url(#rbOscuro)""/>
<rect x=""112"" y=""98"" width=""176"" height=""118"" rx=""38"" fill=""url(#rbMetal)""/>
<rect x=""100"" y=""132"" width=""18"" height=""46"" rx=""8"" fill=""url(#rbOscuro)""/><rect x=""282"" y=""132"" width=""18"" height=""46"" rx=""8"" fill=""url(#rbOscuro)""/>
<rect x=""128"" y=""128"" width=""144"" height=""60"" rx=""26"" fill=""#0C1A2C""/>
<rect x=""136"" y=""138"" width=""128"" height=""40"" rx=""20"" fill=""#6BE8FF"" opacity=""0.55"" filter=""url(#rbBrillo)""/>
<rect x=""136"" y=""138"" width=""128"" height=""40"" rx=""20"" fill=""url(#rbVisor)""/>
<path d=""M150 146H198L184 170H146C140 160 142 150 150 146Z"" fill=""#FFFFFF"" opacity=""0.42""/>
<path d=""M100 112C102 44 154 12 200 12C246 12 298 44 300 112Z"" fill=""url(#rbCasco)""/>
<path d=""M186 14C188 12 212 12 214 14C222 40 222 86 216 110H184C178 86 178 40 186 14Z"" fill=""#FFD84A""/>
<path d=""M186 14C188 12 212 12 214 14L216 30H184Z"" fill=""#FFF2A8""/>
<path d=""M64 124C64 108 86 100 112 100H288C314 100 336 108 336 124C336 134 326 136 314 134C282 126 118 126 86 134C74 136 64 134 64 124Z"" fill=""url(#rbAla)""/>
<path d=""M172 44H228V76C228 92 212 102 200 106C188 102 172 92 172 76Z"" fill=""#E4322B"" stroke=""#FFF2A8"" stroke-width=""4""/>
<path d=""M200 54C204 66 216 70 216 82A16 16 0 0 1 184 82C184 74 190 70 192 64C194 68 196 70 198 70C198 64 198 60 200 54Z"" fill=""#FFD23F""/>
<path d=""M116 60C130 34 154 24 172 22C152 34 138 52 132 76Z"" fill=""#FFFFFF"" opacity=""0.28""/>
";

    public static string RobotSuelto() =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"512\" height=\"512\" viewBox=\"-40 -5 490 490\">\n<defs>" + RobotDefs() + "</defs>\n" + Robot() + "</svg>\n";

    public static string FuegoSuelto() => @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""512"" height=""512"" viewBox=""0 0 512 512"">
<defs>
<radialGradient id=""aura"" gradientUnits=""userSpaceOnUse"" cx=""256"" cy=""320"" r=""250""><stop offset=""0"" stop-color=""#FF7A2F"" stop-opacity="".55""/><stop offset="".6"" stop-color=""#FF5A1F"" stop-opacity="".18""/><stop offset=""1"" stop-color=""#FF5A1F"" stop-opacity=""0""/></radialGradient>
<linearGradient id=""ext"" gradientUnits=""userSpaceOnUse"" x1=""0"" y1=""40"" x2=""0"" y2=""490""><stop offset=""0"" stop-color=""#FF8A1F""/><stop offset="".6"" stop-color=""#F2481B""/><stop offset=""1"" stop-color=""#C22410""/></linearGradient>
<linearGradient id=""med"" gradientUnits=""userSpaceOnUse"" x1=""0"" y1=""150"" x2=""0"" y2=""490""><stop offset=""0"" stop-color=""#FFB02E""/><stop offset=""1"" stop-color=""#FF6A1F""/></linearGradient>
<linearGradient id=""int"" gradientUnits=""userSpaceOnUse"" x1=""0"" y1=""270"" x2=""0"" y2=""490""><stop offset=""0"" stop-color=""#FFE27A""/><stop offset=""1"" stop-color=""#FFB02E""/></linearGradient>
<linearGradient id=""nuc"" gradientUnits=""userSpaceOnUse"" x1=""0"" y1=""350"" x2=""0"" y2=""486""><stop offset=""0"" stop-color=""#FFF2BC""/><stop offset=""1"" stop-color=""#FFD860""/></linearGradient>
</defs>
<circle cx=""256"" cy=""320"" r=""250"" fill=""url(#aura)""/>
<path d=""M268 28C276 96 332 138 372 200C402 248 420 294 416 348C410 432 340 492 256 492C172 492 100 432 96 350C92 288 122 240 158 194C164 234 184 262 212 276C200 200 218 96 268 28Z"" fill=""url(#ext)""/>
<path d=""M268 150C278 204 324 240 346 292C362 330 358 372 340 412C322 456 292 486 256 486C208 486 164 450 158 396C152 346 182 312 204 276C210 308 224 328 246 336C238 280 242 204 268 150Z"" fill=""url(#med)""/>
<path d=""M264 268C272 306 306 330 318 368C330 412 300 480 256 482C212 482 188 442 192 400C196 368 214 348 228 326C232 346 240 358 252 364C250 330 252 296 264 268Z"" fill=""url(#int)""/>
<path d=""M260 356C264 384 288 396 290 424C292 452 276 478 254 478C232 478 218 456 220 432C222 414 234 404 240 390C242 400 246 406 252 408C252 392 254 372 260 356Z"" fill=""url(#nuc)""/>
<circle cx=""128"" cy=""118"" r=""7"" fill=""#FFB347""/><circle cx=""396"" cy=""96"" r=""5"" fill=""#FFD27A""/><circle cx=""92"" cy=""220"" r=""4.5"" fill=""#FF8A3C""/><circle cx=""440"" cy=""206"" r=""6"" fill=""#FFB347""/><circle cx=""178"" cy=""60"" r=""4"" fill=""#FFD27A""/>
</svg>
";
}
