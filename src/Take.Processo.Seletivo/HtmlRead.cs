using System.IO;
using System.Text;

namespace Take.Processo.Seletivo
{
    public static class HtmlRead
    {
        public static string Get() =>
            new StreamReader(Directory.GetCurrentDirectory() + "\\Page\\index.html", Encoding.Default).ReadToEnd();

    }
}
