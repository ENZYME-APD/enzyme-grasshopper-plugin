with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

# Fix Environment ambiguity
ts = ts.replace("Environment.GetFolderPath(Environment.SpecialFolder.Desktop)", "System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)")

# Fix PopViewInfo
old_pop = "activeView.ActiveViewport.PopViewInfo();"
new_pop = "activeView.ActiveViewport.PushViewInfo(originalViewInfo, false);"
ts = ts.replace(old_pop, new_pop)

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
