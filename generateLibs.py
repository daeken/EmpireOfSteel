from glob import glob
import json, os, uuid
from cxxfilt import demangle

csprojTemplate = r'''<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net7.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>disable</Nullable>
    </PropertyGroup>

    <ItemGroup>
      <ProjectReference Include="..\Common\Common.csproj" />
    </ItemGroup>

</Project>'''

for name in glob('ps4libdoc/*.json'):
	data = file(name).read().decode('utf-8')
	if data[0] == u'\ufeff':
		data = data[1:]
	tree = json.loads(data)
	module = tree['modules'][0]
	name = module['name']
	name = name[0].upper() + name[1:]
	try:
		os.mkdir(name)
	except:
		pass
	print r'''Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "%s", "%s\%s.csproj", "{%s}"
EndProject''' % (name, name, name, str(uuid.uuid4()).upper())
	with file(name + '/' + name + '.csproj', 'w') as fp:
		fp.write(csprojTemplate)
	for lib in module['libraries']:
		if 'is_export' in lib and not lib['is_export']:
			continue
		libname = lib['name']
		cslibname = ''.join(elem[0].upper() + elem[1:] if len(elem) else elem for elem in libname.split('_'))
		with file(name + '/' + cslibname + '.cs', 'w') as fp:
			print >>fp, 'namespace %s;' % name
			print >>fp, 'using Common;'
			print >>fp
			print >>fp, '[Library("%s")]' % libname
			print >>fp, 'public static class %s {' % cslibname
			symbols = [sym for sym in lib['symbols'] if 'type' not in sym or sym['type'] == 'Function']
			unkI = [0]
			def unknown():
				unkI[0] += 1
				return 'Unknown%i' % (unkI[0] - 1)
			def retitle(name):
				if name[0] != '_':
					return name[0].upper() + name[1:]
				return name
			for sym in symbols:
				print >>fp, '\t[Export("%s")]' % sym['encoded_id']
				if 'name' in sym and sym['name'] is not None:
					try:
						demangled = demangle(sym['name'])
						if demangled != sym['name']:
							print >>fp, '\t// Demangled:', demangled
					except:
						pass
				print >>fp, '\tpublic static void %s() => throw new NotImplementedException();' % (retitle(sym['name']) if 'name' in sym and sym['name'] is not None else unknown())
			print >>fp, '}'
