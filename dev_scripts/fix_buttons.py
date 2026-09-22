with open("Components/ContactEnzyme.cs", "r") as f:
    text = f.read()

text = text.replace('System.Diagnostics.Process.Start("https://www.weareenzyme.com/");',
                    'var psi = new System.Diagnostics.ProcessStartInfo("https://www.weareenzyme.com/") { UseShellExecute = true };\n                        System.Diagnostics.Process.Start(psi);')

text = text.replace('System.Diagnostics.Process.Start(url);',
                    'var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };\n                        System.Diagnostics.Process.Start(psi);')

with open("Components/ContactEnzyme.cs", "w") as f:
    f.write(text)
