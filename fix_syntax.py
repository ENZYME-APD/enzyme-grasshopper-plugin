with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

ts = ts.replace('new GH_ValueListItem(mode.EnglishName, $""{mode.EnglishName}"")' , 'new GH_ValueListItem(mode.EnglishName, $"\\"{mode.EnglishName}\\"")')
ts = ts.replace('new GH_ValueListItem(n, $""{n}"")' , 'new GH_ValueListItem(n, $"\\"{n}\\"")')

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
