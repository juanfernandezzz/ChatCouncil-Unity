using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace ChatCouncil.Motor.Pruebas
{
    /// <summary>D69: el traslado de la carpeta de datos, siempre sobre carpetas temporales, nunca la real.</summary>
    public class TrasladoPruebas
    {
        string padre, vieja, nueva;

        [SetUp]
        public void Preparar()
        {
            padre = Path.Combine(Path.GetTempPath(), "cc-traslado-" + Guid.NewGuid().ToString("N"));
            vieja = Path.Combine(padre, "ChatCouncil");
            nueva = Path.Combine(padre, Traslado.NombreCarpeta);
            Directory.CreateDirectory(vieja);
        }

        [TearDown]
        public void Limpiar() => Directory.Delete(padre, true);

        void SembrarDatosDeLaApp()
        {
            Directory.CreateDirectory(Path.Combine(vieja, "conversaciones"));
            File.WriteAllText(Path.Combine(vieja, "conversaciones", "c1.jsonl"), "{\"tipo\":\"x\"}\n");
            Directory.CreateDirectory(Path.Combine(vieja, "informes", "2026-11-10 0036 - Informe"));
            File.WriteAllText(Path.Combine(vieja, "informes", "2026-11-10 0036 - Informe", "informe.md"), "# informe");
            Directory.CreateDirectory(Path.Combine(vieja, "navegador", "EBWebView"));
            File.WriteAllText(Path.Combine(vieja, "navegador", "EBWebView", "Local State"), "no se lee");
            File.WriteAllText(Path.Combine(vieja, "apariencia.json"), "{\"tema\":\"oscuro\"}");
            File.WriteAllText(Path.Combine(vieja, Roles.ArchivoSeleccion), "{}");
            // Del motor de Unity: se quedan.
            File.WriteAllText(Path.Combine(vieja, "Player.log"), "log");
            File.WriteAllText(Path.Combine(vieja, "Player-prev.log"), "log previo");
        }

        static string[] Contenido(string raiz) =>
            Directory.GetFileSystemEntries(raiz, "*", SearchOption.AllDirectories)
                .Select(r => r.Substring(raiz.Length + 1) + (File.Exists(r) ? "=" + File.ReadAllText(r) : "/"))
                .OrderBy(r => r, StringComparer.Ordinal).ToArray();

        [Test]
        public void MueveLasEntradasDeLaAppYDejaLasDelMotor()
        {
            SembrarDatosDeLaApp();
            var antes = Contenido(vieja).Where(r => !r.StartsWith("Player", StringComparison.Ordinal)).ToArray();

            var raiz = Traslado.RaizVigente(vieja);

            Assert.That(raiz, Is.EqualTo(nueva));
            Assert.That(Contenido(nueva), Is.EqualTo(antes));
            Assert.That(Directory.GetFileSystemEntries(vieja).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal),
                Is.EqualTo(new[] { "Player-prev.log", "Player.log" }));
        }

        [Test]
        public void SiLaRaizNuevaYaExisteNoMueveNadaYLaUsa()
        {
            SembrarDatosDeLaApp();
            Directory.CreateDirectory(nueva);
            File.WriteAllText(Path.Combine(nueva, "apariencia.json"), "{\"tema\":\"claro\"}");
            var antesVieja = Contenido(vieja);

            var raiz = Traslado.RaizVigente(vieja);

            Assert.That(raiz, Is.EqualTo(nueva));
            Assert.That(Contenido(vieja), Is.EqualTo(antesVieja));
            Assert.That(Contenido(nueva), Is.EqualTo(new[] { "apariencia.json={\"tema\":\"claro\"}" }));
        }

        [Test]
        public void UnFalloAMitadDeCaminoDevuelveTodoYSigueConLaRaizVieja()
        {
            SembrarDatosDeLaApp();
            var antes = Contenido(vieja);
            var movidas = 0;
            void MoverQueFalla(string de, string a)
            {
                if (a.StartsWith(nueva, StringComparison.Ordinal) && ++movidas == 3) throw new IOException("archivo en uso (simulado)");
                Traslado.Mover(de, a);
            }

            var raiz = Traslado.RaizVigente(vieja, MoverQueFalla, () => new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

            Assert.That(raiz, Is.EqualTo(vieja));
            Assert.That(Directory.Exists(nueva), Is.False, "la raíz nueva quedó vacía y se borra");
            var diagnosticos = Path.Combine(vieja, "diagnosticos");
            var registro = Directory.GetFiles(diagnosticos).Single();
            Assert.That(Path.GetFileName(registro), Is.EqualTo("traslado-20261110-090000.json"));
            Assert.That(File.ReadAllText(registro), Does.Contain("archivo en uso (simulado)"));
            Assert.That(Contenido(vieja).Where(r => !r.StartsWith("diagnosticos", StringComparison.Ordinal)), Is.EqualTo(antes));
        }

        [Test]
        public void ArranqueLimpioUsaLaRaizNueva()
        {
            var raiz = Traslado.RaizVigente(vieja);

            Assert.That(raiz, Is.EqualTo(nueva));
            Assert.That(Directory.Exists(nueva), Is.True);
            Assert.That(Directory.GetFileSystemEntries(nueva), Is.Empty);
        }

        [Test]
        public void AceptaLaRutaDeUnityConBarrasNormales() =>
            Assert.That(Traslado.RaizVigente(vieja.Replace('\\', '/')), Is.EqualTo(nueva));
    }
}
