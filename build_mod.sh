dotnet build -c Release
rm -rf local_mod/*
cp -r About local_mod/
cp bin/Release/net46/IC10Editor.dll local_mod/
