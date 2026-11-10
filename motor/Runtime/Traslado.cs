using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ChatCouncil.Motor
{
    /// <summary>
    /// D69: los datos de la app pasan de la carpeta de Unity (persistentDataPath) a su hermana «ChatCouncil-Unity»,
    /// para convivir con la app nueva de ChatCouncil. Se mueve, nunca se copia ni se lee: el perfil del navegador
    /// viaja entero sin abrirlo. Lo del motor de Unity (Player.log, Player-prev.log) se queda donde está.
    /// </summary>
    public static class Traslado
    {
        public const string NombreCarpeta = "ChatCouncil-Unity";

        /// <summary>Lo que la app escribe en su carpeta de datos (ver Aplicacion.cs, Registro.cs, Puerta.cs, PantallaRonda.cs, Configuracion.cs).</summary>
        public static readonly string[] Entradas =
        {
            "conversaciones", "informes", "operaciones", "redactor", "diagnosticos", "navegador",
            "apariencia.json", Roles.ArchivoSeleccion, "specs.json",
        };

        /// <summary>
        /// La raíz que usa la app. Si la nueva ya existe, esa. Si no, la crea y mueve ahí cada entrada de la app; si un
        /// movimiento falla, devuelve las ya movidas, borra la nueva solo si quedó vacía, lo anota en diagnósticos y
        /// sigue con la vieja.
        /// </summary>
        public static string RaizVigente(string raizVieja, Action<string, string> mover = null, Func<DateTimeOffset> ahora = null)
        {
            raizVieja = Path.GetFullPath(raizVieja).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var raizNueva = Path.Combine(Path.GetDirectoryName(raizVieja), NombreCarpeta);
            if (Directory.Exists(raizNueva)) return raizNueva;

            mover ??= Mover;
            var presentes = Entradas.Where(e => Directory.Exists(Path.Combine(raizVieja, e)) || File.Exists(Path.Combine(raizVieja, e))).ToList();
            Directory.CreateDirectory(raizNueva);
            var movidas = new List<string>();
            try
            {
                foreach (var e in presentes)
                {
                    mover(Path.Combine(raizVieja, e), Path.Combine(raizNueva, e));
                    movidas.Add(e);
                }
                return raizNueva;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                var sinDevolver = new List<string>();
                foreach (var e in movidas)
                    try { mover(Path.Combine(raizNueva, e), Path.Combine(raizVieja, e)); }
                    catch (Exception ex2) when (ex2 is IOException || ex2 is UnauthorizedAccessException) { sinDevolver.Add(e + ": " + ex2.Message); }
                if (!Directory.EnumerateFileSystemEntries(raizNueva).Any()) Directory.Delete(raizNueva);

                var dir = Path.Combine(raizVieja, "diagnosticos");
                Directory.CreateDirectory(dir);
                var cuando = (ahora ?? (() => DateTimeOffset.Now))();
                File.WriteAllText(Path.Combine(dir, "traslado-" + cuando.ToString("yyyyMMdd-HHmmss") + ".json"), new JObject
                {
                    ["traslado"] = "fallido; la app sigue con la carpeta de siempre",
                    ["de"] = raizVieja,
                    ["a"] = raizNueva,
                    ["error"] = ex.Message,
                    ["devueltas"] = new JArray(movidas.Except(sinDevolver.Select(s => s.Split(':')[0])).ToArray()),
                    ["sinDevolver"] = new JArray(sinDevolver.ToArray()),
                    ["cuando"] = cuando.ToString("o"),
                }.ToString(), new System.Text.UTF8Encoding(false));
                return raizVieja;
            }
        }

        public static void Mover(string de, string a)
        {
            if (Directory.Exists(de)) Directory.Move(de, a);
            else File.Move(de, a);
        }
    }
}
