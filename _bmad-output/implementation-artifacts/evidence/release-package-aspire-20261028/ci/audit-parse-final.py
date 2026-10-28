import pathlib,json,xml.etree.ElementTree as ET,hashlib,datetime
P=pathlib.Path('/tmp/timesheets-release-evidence-20261028/ci')
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
methods=['AdminStateLoadsUseTheHostsHttpClaimsAccessor','AdminRequestReadSetExcludesDemonstratedPromotedCatalogCleanup','IssuanceWithMissingCapabilityIdIsOpaqueAndPerformsNoProtectedWork']
expected=['Hexalith.Timesheets.ArchitectureTests','Hexalith.Timesheets.Contracts.Tests','Hexalith.Timesheets.Server.Tests','Hexalith.Timesheets.Projections.Tests','Hexalith.Timesheets.Works.Tests','Hexalith.Timesheets.IntegrationTests']
results=[]
for name in expected:
    matches=list((P/'audit-blocking-test-results').rglob(name+'.trx'))
    if len(matches)!=1: raise ValueError((name,len(matches)))
    f=matches[0]; root=ET.parse(f).getroot(); rs=root.find('t:ResultSummary',ns)
    counts={k:int(v) for k,v in rs.find('t:Counters',ns).attrib.items()}
    rows=[dict(e.attrib) for e in root.findall('t:Results/t:UnitTestResult',ns)]
    times=dict(root.find('t:Times',ns).attrib)
    claims=[x for x in rows if any(m in x.get('testName','') for m in methods)]
    results.append({'suite':name,'file':str(f.relative_to(P)),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'summary':dict(rs.attrib),'counters':counts,'times':times,'result_rows':len(rows),'outcome_counts':{o:sum(x.get('outcome')==o for x in rows) for o in sorted({x['outcome'] for x in rows})},'claims_rows':claims,'skipped_rows':[x for x in rows if x.get('outcome')=='NotExecuted'],'other_rows':[x for x in rows if x.get('outcome') not in ['Passed','NotExecuted']]})
totals={k:sum(r['counters'].get(k,0) for r in results) for k in sorted({k for r in results for k in r['counters']})}
claims=[x for r in results for x in r['claims_rows']]
chronological=sorted(results,key=lambda x:x['times']['start'])
intervals=[{'earlier':a['suite'],'finish':a['times']['finish'],'later':b['suite'],'start':b['times']['start'],'non_overlapping':datetime.datetime.fromisoformat(a['times']['finish'])<=datetime.datetime.fromisoformat(b['times']['start'])} for a,b in zip(chronological,chronological[1:])]
summary={'source_commit':'94d0a6cd1182b6c9f3ee3392e016cab4004eb3b8','source_run':40670628561,'suites':results,'totals':totals,'chronological_suite_order':[r['suite'] for r in chronological],'serial_intervals':intervals,'no_overlap':all(x['non_overlapping'] for x in intervals),'claims_count':len(claims),'claims_method_counts':{m:sum(m in x.get('testName','') for x in claims) for m in methods},'claims_all_passed':len(claims)==9 and all(x['outcome']=='Passed' for x in claims)}
(P/'audit-final-suite-audit.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:v for k,v in summary.items() if k!='suites'},indent=2))
