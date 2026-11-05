"""Synthetic GHX pair: N components in layered columns, each with 2 inputs wired to earlier outputs,
~10% modified (value/nickname/script), ~10% added, ~10% removed, some rewires, a few clusters (fixture payload), groups."""
import random,uuid,sys,re
N=int(sys.argv[1]); out=sys.argv[2]; rnd=random.Random(N)
fx=open('Tests/data/cluster-old.ghx').read(); fxn=open('Tests/data/cluster-new.ghx').read()
cl_old=re.findall(r'<item name="ClusterDocument">\s*<stream>(.*?)</stream>',fx,re.S)[0] if 'ClusterDocument' in fx else 'H4sIAAAAAAAEAO29B2AcSZYlJi9tynt/SvVK1+B0oQiAYBMk2JBAEOzBiM3mkuwdaUcjKasqgcplVmVdZhZAzO2dvPfee++999577733ujudTif33/8/XGZkAWz2zkrayZ4hgKrIHz9+fB8/IorZ7r8='
cl_new=re.findall(r'<item name="ClusterDocument">\s*<stream>(.*?)</stream>',fxn,re.S)[0] if 'ClusterDocument' in fxn else cl_old
G=lambda:str(uuid.UUID(int=rnd.getrandbits(128)))
def item(n,v,t='gh_string',c=10,idx=None): return f'<item name="{n}"{f" index=\"{idx}\"" if idx is not None else ""} type_name="{t}" type_code="{c}">{v}</item>'
def bounds(x,y,w,h): return f'<item name="Bounds" type_name="gh_drawing_rectanglef" type_code="35"><X>{x}</X><Y>{y}</Y><W>{w}</W><H>{h}</H></item>'
def param(kind,i,pid,name,srcs,val=None):
    s=''.join(item('Source',x,'gh_guid',9,k) for k,x in enumerate(srcs))
    pd=f'<chunk name="PersistentData"><items count="1">{item("Count",1,"gh_int32",3)}</items><chunks count="1"><chunk name="Branch" index="0"><items count="2">{item("Count",1,"gh_int32",3)}{item("Path","{0}")}</items><chunks count="1"><chunk name="Item" index="0"><items count="1">{item("number",val,"gh_double",6)}</items></chunk></chunks></chunk></chunks></chunk>' if val is not None else ''
    return f'<chunk name="{kind}" index="{i}"><items>{item("InstanceGuid",pid,"gh_guid",9)}{item("Name",name)}{item("NickName",name[0])}{s}{item("SourceCount",len(srcs),"gh_int32",3)}</items><chunks><chunk name="Attributes"><items>{bounds(0,0,10,10)}</items></chunk>{pd}</chunks></chunk>'
nodes=[]
cols=max(4,int(N**0.5))
for i in range(N):
    c=i%cols; r=i//cols
    nd=dict(g=G(),i=i,x=100+c*220,y=100+r*70,name=rnd.choice(['Addition','Multiplication','Move','Rotate','Series','Range','Construct Point','Line','Divide Curve','Loft']),
            ins=[G(),G()],out=G(),vals=[round(rnd.uniform(0,10),3),round(rnd.uniform(0,10),3)],cluster=(i%97==50),script=(i%53==20))
    nodes.append(nd)
for nd in nodes:
    prev=[p for p in nodes[:nd['i']] if p['i']%cols < nd['i']%cols] or nodes[:nd['i']]
    nd['src']=[[rnd.choice(prev)['out']] if prev and rnd.random()<0.85 else [] for _ in range(2)]
def obj(nd,ver):
    if nd['cluster']:
        return f'<chunk name="Object"><items>{item("GUID","f31d8d7a-7536-4ac8-9c96-fde6ecda4d0a","gh_guid",9)}{item("Name","Cluster")}<item name="ClusterDocument"><stream>{cl_old if ver=="old" else cl_new}</stream></item></items><chunks><chunk name="Container"><items>{item("InstanceGuid",nd["g"],"gh_guid",9)}{item("Name","Cluster")}{item("NickName","Cluster"+str(nd["i"]))}</items><chunks><chunk name="Attributes"><items>{bounds(nd["x"],nd["y"],90,30)}</items></chunk>{param("param_input",0,nd["ins"][0],"In",nd["src"][0])}{param("param_output",0,nd["out"],"Out",[])}</chunks></chunk></chunks></chunk>'
    extra=''
    if nd['script']:
        code=f'int a = {nd["i"]};\nreturn a * 2;' if ver=='old' else f'int a = {nd["i"]};\nint b = a + 1;\nreturn b * 3;'
        extra=item('ScriptSource',code)
    nick=nd.get('nick_'+ver,nd['name'][:4])
    ps=''.join(param('param_input',k,nd['ins'][k],'AB'[k],nd['src_'+ver][k] if 'src_'+ver in nd else nd['src'][k],nd['vals_'+ver][k] if 'vals_'+ver in nd else nd['vals'][k]) for k in range(2))
    return f'<chunk name="Object"><items>{item("GUID",str(uuid.uuid5(uuid.NAMESPACE_DNS,nd["name"])),"gh_guid",9)}{item("Name",nd["name"])}</items><chunks><chunk name="Container"><items>{item("InstanceGuid",nd["g"],"gh_guid",9)}{item("Name",nd["name"])}{item("NickName",nick)}{extra}</items><chunks><chunk name="Attributes"><items>{bounds(nd["x"],nd["y"],120,44)}</items></chunk>{ps}{param("param_output",0,nd["out"],"R",[])}</chunks></chunk></chunks></chunk>'
idx=list(range(N)); rnd.shuffle(idx); k=N//10
mod,add,rem=idx[:k],set(idx[k:2*k]),set(idx[2*k:3*k])
for i in add: nodes[i]['name']=rnd.choice(['Panel','Boolean Toggle','Graph Mapper','Gradient'])
for i in rem: nodes[i]['name']=rnd.choice(['Flip Matrix','Shift List','Cull Pattern','Dispatch'])
for i in mod:
    nd=nodes[i]; t=rnd.random()
    if t<0.5: nd['vals_new']=[nd['vals'][0]+1,nd['vals'][1]]
    elif t<0.8: nd['nick_new']='Mod'+str(i)
    else: nd['src_new']=[nd['src'][1],nd['src'][0]]
    nd['script']=nd['script'] or False
def doc(ver):
    objs=[obj(n,ver) for n in nodes if not ((ver=='old' and n['i'] in add) or (ver=='new' and n['i'] in rem))]
    return f'<?xml version="1.0" encoding="utf-8" standalone="yes"?><Archive name="Root"><items>{item("ArchiveVersion","")}</items><chunks><chunk name="Definition"><items>{item("plugin_version","")}</items><chunks><chunk name="DefinitionProperties"><items>{item("Name",f"Synthetic {N}")}{item("Description","synthetic")}</items></chunk><chunk name="DefinitionObjects"><items>{item("ObjectCount",len(objs),"gh_int32",3)}</items><chunks count="{len(objs)}">{"".join(objs)}</chunks></chunk></chunks></chunk></chunks></Archive>'
open(out+'-old.ghx','w').write(doc('old')); open(out+'-new.ghx','w').write(doc('new'))
