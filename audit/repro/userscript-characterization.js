// Run: node audit/repro/userscript-characterization.js "Addons/reddit video download button.user.js"
// Execute the repository userscript itself against a minimal old-Reddit-shaped DOM.
// Characterization of an existing defect, NOT a remediation or browser-installation test.
const fs = require('fs');
const vm = require('vm');
if (!process.argv[2]) throw new Error('Pass the repository userscript path.');
const source = fs.readFileSync(process.argv[2], 'utf8');
function element(tag) { return {tag, children:[], append(x){this.children.push(x)}, appendChild(x){this.children.push(x)}}; }
function run(url, count) {
 const tags = Array.from({length:count}, () => element('ul'));
 const document = {URL:url, getElementsByClassName(name){return name==='reddit-video-player-root' ? Array.from({length:count}, ()=>({})) : tags;}, createElement:element, createTextNode(text){return {text}}};
 vm.runInNewContext(source, {document}, {timeout: 1000});
 return {url, per_post_button_counts:tags.map(t=>t.children.length), links:tags.flatMap(t=>t.children.map(li=>li.children[0].href))};
}
const single = run('https://old.reddit.com/r/example/comments/abc/one/',1);
const listing = run('https://old.reddit.com/r/example/',2);
if (single.per_post_button_counts[0]!==3 || listing.per_post_button_counts.join(',')!=='3,0' || !listing.links.every(x=>x.includes(listing.url))) throw Error('Observation not established; inspect behavior instead of claiming reproduction.');
console.log(JSON.stringify({purpose:'Existing defect characterization; browser installation not tested',single,listing},null,2));
