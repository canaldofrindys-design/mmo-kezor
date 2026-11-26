# Kezor — andar e correr com captura de movimento real (CMU Graphics Lab Motion Capture Database, conversão BVH de
# Bruce Hahne / cgspeed; uso livre). Transfere o clipe para o esqueleto de 15 ossos do herói (kezor_anim.BONES) e
# substitui os clipes Walk/Run (e as passadas) em Assets/Resources/Anim/Heroi.txt, mantendo os outros clipes.
#
# Roda em Python comum (numpy + scipy), fora do Blender:
#   python kezor_mocap.py Heroi.txt 35_01.bvh 09_01.bvh
# Clipes usados: Walk = CMU 35_01 (ciclo a partir do quadro 29, 135 quadros a 120 fps); Run = CMU 09_01 (57, 89).
# Os BVH vêm de https://github.com/una-dinosauria/cmu-mocap (data/035/35_01.bvh, data/009/09_01.bvh).
#
# Como funciona: cada osso nosso recebe a rotação global do osso do mocap em relação à pose T do quadro 1, vezes a
# rotação mínima que leva a direção de repouso do nosso osso (braços para baixo) à direção na pose T. O ciclo é
# girado para andar para a frente (-Y), o laço é fechado distribuindo a diferença entre o fim e o início, o quadril é
# ajustado para o pé de apoio tocar o chão e a passada é medida pelo pé de apoio (o jogo sincroniza a fase por ela).
import sys, pickle
import numpy as np
from scipy.spatial.transform import Rotation as R, Slerp
from scipy.spatial.transform import Rotation as R
class Joint:
    def __init__(s,name,parent): s.name=name; s.parent=parent; s.offset=np.zeros(3); s.channels=[]; s.children=[]
def load(path):
    txt=open(path).read().split('MOTION')
    toks=txt[0].split()
    joints=[]; stack=[]; i=0; cur=None
    while i<len(toks):
        t=toks[i]
        if t in('ROOT','JOINT'):
            j=Joint(toks[i+1], stack[-1] if stack else None); joints.append(j)
            if j.parent: j.parent.children.append(j)
            cur=j; i+=2
        elif t=='End': cur=None; i+=2; depth=0
        elif t=='{': stack.append(cur); i+=1
        elif t=='}': stack.pop(); i+=1
        elif t=='OFFSET':
            if cur is not None: cur.offset=np.array(list(map(float,toks[i+1:i+4])))
            i+=4
        elif t=='CHANNELS':
            n=int(toks[i+1]); cur.channels=toks[i+2:i+2+n]; i+=2+n
        else: i+=1
    m=txt[1].split('\n')
    nf=int(m[1].split()[1]); ft=float(m[2].split()[2])
    data=np.array([list(map(float,l.split())) for l in m[3:3+nf]])
    return joints,data,ft
def fk(joints,data):
    """returns dict name->(pos[N,3], Rotation global array)"""
    N=len(data); col=0; out={}
    for j in joints:
        ch=j.channels; vals=data[:,col:col+len(ch)]; col+=len(ch)
        pos=np.zeros((N,3)); rot_order=''; angs=[]
        for k,c in enumerate(ch):
            if c.endswith('position'): pos['XYZ'.index(c[0])]  # noop
        p_idx=[k for k,c in enumerate(ch) if c.endswith('position')]
        r_idx=[k for k,c in enumerate(ch) if c.endswith('rotation')]
        order=''.join(ch[k][0] for k in r_idx)
        Rl=R.from_euler(order, vals[:,r_idx], degrees=True)  # uppercase=intrinsic
        if j.parent is None:
            P=vals[:,p_idx]+j.offset; G=Rl
        else:
            pp,pg=out[j.parent.name]
            P=pp+pg.apply(np.tile(j.offset,(N,1))); G=pg*Rl
        out[j.name]=(P,G)
    return out

OUR = ['Body','Chest','Head','ArmL','ForeArmL','HandL','ArmR','ForeArmR','HandR','LegL','ShinL','FootL','LegR','ShinR','FootR']
HEAD = {'Body':(0,0,0.95),'Chest':(0,0,1.05),'Head':(0,0,1.47),'ArmL':(0.31,0,1.40),'ForeArmL':(0.31,0,1.08),'HandL':(0.31,0,0.80),
        'ArmR':(-0.31,0,1.40),'ForeArmR':(-0.31,0,1.08),'HandR':(-0.31,0,0.80),'LegL':(0.12,0,0.92),'ShinL':(0.12,0,0.53),'FootL':(0.12,0,0.10),
        'LegR':(-0.12,0,0.92),'ShinR':(-0.12,0,0.53),'FootR':(-0.12,0,0.10)}
TAIL = {'ArmL':(0.31,0,1.08),'ForeArmL':(0.31,0,0.80),'HandL':(0.31,0,0.70),'ArmR':(-0.31,0,1.08),'ForeArmR':(-0.31,0,0.80),'HandR':(-0.31,0,0.70),
        'LegL':(0.12,0,0.53),'ShinL':(0.12,0,0.10),'FootL':(0.12,-0.15,0.03),'LegR':(-0.12,0,0.53),'ShinR':(-0.12,0,0.10),'FootR':(-0.12,-0.15,0.03)}
# osso nosso -> (junta do mocap cuja rotação global vale, junta filha que dá a direção)
MAP = {'Body':('Hips',None),'Chest':('Spine1',None),'Head':('Head',None),
       'ArmL':('LeftArm','LeftForeArm'),'ForeArmL':('LeftForeArm','LeftHand'),'HandL':('LeftHand','LeftHandIndex1'),
       'ArmR':('RightArm','RightForeArm'),'ForeArmR':('RightForeArm','RightHand'),'HandR':('RightHand','RightHandIndex1'),
       'LegL':('LeftUpLeg','LeftLeg'),'ShinL':('LeftLeg','LeftFoot'),'FootL':('LeftFoot','LeftToeBase'),
       'LegR':('RightUpLeg','RightLeg'),'ShinR':('RightLeg','RightFoot'),'FootR':('RightFoot','RightToeBase')}
M = np.array([[1,0,0],[0,0,-1],[0,1,0]],float)   # BVH (Y cima, frente +Z) -> Blender (Z cima, frente -Y)
MR = R.from_matrix(M)

def minrot(a,b):
    a=a/np.linalg.norm(a); b=b/np.linalg.norm(b)
    v=np.cross(a,b); s=np.linalg.norm(v); c=a@b
    if s<1e-8: return R.identity()
    return R.from_rotvec(v/s*np.arctan2(s,c))

def retarget(path, start, period, fps_out=30, our_leg=0.82):
    J,D,ft = load(path)
    o = fk(J,D)
    P = {k:(v[0]@M.T) for k,v in o.items()}
    G = {k:(MR*v[1]*MR.inv()) for k,v in o.items()}
    leg = np.linalg.norm(P['LeftUpLeg'][0]-P['LeftLeg'][0])+np.linalg.norm(P['LeftLeg'][0]-P['LeftFoot'][0])
    sc = our_leg/leg
    a, T = start, period
    hip = P['Hips']
    d = hip[a+T]-hip[a]; d[2]=0
    yaw = np.arctan2(d[0], -d[1])              # ângulo de d em relação a -Y
    Qy = R.from_rotvec([0,0,-yaw])              # leva d para -Y
    stride = np.linalg.norm(d)*sc
    C = {}
    for n in OUR:
        j,c = MAP[n]
        if c is None: C[n]=R.identity(); continue
        C[n] = minrot(np.array(TAIL[n])-np.array(HEAD[n]), P[c][0]-P[j][0])
    G0inv = {MAP[n][0]: G[MAP[n][0]][0].inv() for n in OUR}
    def raw(i):
        rots = [Qy*G[MAP[n][0]][i]*G0inv[MAP[n][0]]*C[n] for n in OUR]
        k = (i-a)/T
        base = hip[a] + (hip[a+T]-hip[a])*k
        off = hip[i]-base
        off[2] = hip[i][2]-hip[0][2]
        h = Qy.apply(off)*sc
        return rots, h
    n_out = int(round(T*ft*fps_out))
    frames=[]
    for f in range(n_out):
        x = a + f*T/n_out
        i0=int(np.floor(x)); k=x-i0
        def samp(i0,k):
            r0,h0=raw(i0); r1,h1=raw(i0+1)
            rs=[Slerp([0,1],R.concatenate([p,q]))(k) for p,q in zip(r0,r1)]
            return rs, h0*(1-k)+h1*k
        rA,hA = samp(i0,k)          # amostra no ciclo
        w = f/n_out
        if a - T >= 1:
            rB,hB = samp(i0-T,k)    # mesma fase no ciclo anterior: fecha o laço
            rs=[Slerp([0,1],R.concatenate([p,q]))(w) for p,q in zip(rA,rB)]
            h = hA*(1-w)+hB*w
        else:                       # só um ciclo gravado: distribui a diferença entre o fim e o início
            r0,h0=raw(a); r1,h1=raw(a+T)
            rs=[Slerp([0,1],R.concatenate([R.identity(), q0*q1.inv()]))(w)*p for p,q0,q1 in zip(rA,r0,r1)]
            h = hA + (h0-h1)*w
        frames.append((h,rs))
    return frames, stride, T*ft, sc

def foot_info(path):
    J,D,ft=load(path); o=fk(J,D)
    P={k:(v[0]@M.T) for k,v in o.items()}
    return P, ft

PARENT = {'Body':None,'Chest':'Body','Head':'Chest','ArmL':'Chest','ForeArmL':'ArmL','HandL':'ForeArmL','ArmR':'Chest','ForeArmR':'ArmR','HandR':'ForeArmR',
          'LegL':'Body','ShinL':'LegL','FootL':'ShinL','LegR':'Body','ShinR':'LegR','FootR':'ShinR'}
def joints(h, rs):
    W = dict(zip(OUR, rs)); pos = {'Body': np.array(HEAD['Body'])+h}
    for n in OUR[1:]:
        p = PARENT[n]
        pos[n] = pos[p] + W[p].apply(np.array(HEAD[n])-np.array(HEAD[p]))
    tails = {n: pos[n] + W[n].apply(np.array(TAIL[n])-np.array(HEAD[n])) for n in TAIL}
    tails['Head'] = pos['Head'] + W['Head'].apply([0,0,0.28])
    return pos, tails

def finalize(frames, dur):
    """Ajusta a altura do quadril (o pé de apoio toca o chão como no repouso) e mede a passada pelo pé de apoio."""
    az = []
    for h, rs in frames:
        pos,_ = joints(h, rs); az.append((pos['FootL'][2], pos['FootR'][2], pos['FootL'][1], pos['FootR'][1]))
    az = np.array(az)
    k = 0.10 - az[:, :2].min()
    out = [(h + np.array([0, 0, k]), rs) for h, rs in frames]
    n = len(frames); dt = dur / n
    vs = []
    for s in (0, 1):
        z = az[:, s]; y = az[:, 2 + s]
        low = z < z.min() + 0.02
        for i in range(n):
            j = (i + 1) % n
            if low[i] and low[j]: vs.append((y[j] - y[i]) / dt)
    v = np.median(vs) if vs else 0
    return out, v * dur, k


# passada no arquivo = passada medida x fator: um pouco mais longa para a cadência não ficar frenética nas
# velocidades do jogo (vila 1,8 m/s; fora 5,5-6 m/s), aceitando um leve deslizar do pé
CHOICE = {'Walk': (29, 135, 1.10), 'Run': (57, 89, 1.20)}


def clip_lines(name, frames, dur):
    L = ['clip %s %d 1 %.4f' % (name, len(frames), dur)]
    for h, rs in frames:
        vals = ['%.4f %.4f %.4f' % tuple(h)]
        for r in rs:
            x, y, z, w = r.as_quat()
            if w < 0:
                x, y, z, w = -x, -y, -z, -w
            vals.append('%.5f %.5f %.5f %.5f' % (w, x, y, z))
        L.append('f ' + ' '.join(vals))
    return L


def main(heroi, walk_bvh, run_bvh):
    new = {}
    for name, path in (('Walk', walk_bvh), ('Run', run_bvh)):
        a, T, fac = CHOICE[name]
        fr, _, dur, _ = retarget(path, a, T)
        fr, stride, _ = finalize(fr, dur)
        new[name] = (clip_lines(name, fr, dur), stride * fac)
    out, skip = [], False
    for l in open(heroi, encoding='utf-8').read().split('\n'):
        if l.startswith('stride ') and l.split()[1] in new:
            n = l.split()[1]; out.append('stride %s %.4f' % (n, new[n][1])); continue
        if l.startswith('clip '):
            n = l.split()[1]
            skip = n in new
            if skip:
                out.extend(new[n][0]); continue
        if skip and l.startswith('f '):
            continue
        out.append(l)
    open(heroi, 'w', encoding='utf-8', newline='\n').write('\n'.join(out))


if __name__ == '__main__':
    main(*sys.argv[1:4])
