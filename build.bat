@echo off
setlocal

@title=Build AutoHistoryFork


rem deleta os arquivos nuget existentes em todos os projetos
del /s /q ".\*.*upkg"
rem build
dotnet build -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg

rem Contador de arquivos
set contador=0

rem Contar os arquivos .nupkg no diretório
for /R ".\Havan.Core\" %%f in (*.nupkg) do (
    set /a contador+=1
)

rem Exibir o número de arquivos encontrados
echo number of files .nupkg: %contador%


endlocal