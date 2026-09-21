# Deployment

Three environments, same images: docker-compose for local work, Kubernetes on a local
cluster, and AWS. The manifests in `deploy/k8s` are the same ones used in all three
Kubernetes cases; only the image registry, the database and the ingress change.

---

## 1. Local — docker-compose

```bash
docker compose -f deploy/compose/docker-compose.yml up --build
```

| Service | URL |
|---|---|
| Admin panel | <http://localhost:8080> |
| API | <http://localhost:5100> |
| Swagger UI | <http://localhost:5100/swagger> |
| PostgreSQL | `localhost:5433` |

The API migrates and seeds itself at startup, so there is no setup step. Browse the shop
without signing in; sign in as `customer` / `customer123` to check out, or `admin` /
`admin123` for the admin area.

> If a compose **service is renamed**, the container from the old name keeps running and
> holds its published port, so the new one fails to start with "port is already allocated".
> Worse, requests still reach the stale container, so a smoke test can pass against old
> code. `docker compose down --remove-orphans` is the fix.

---

## 2. Local Kubernetes — minikube

This is the path the manifests were developed and verified against.

```bash
minikube start --driver=docker
minikube addons enable ingress

# Build the images, then tag them for the cluster.
docker compose -f deploy/compose/docker-compose.yml build
docker tag orderflow-api:latest          orderflow-api:v1
docker tag orderflow-web:latest  orderflow-web:v1

# minikube has its own container runtime; images must be loaded into it.
minikube image load orderflow-api:v1
minikube image load orderflow-web:v1

kubectl apply -k deploy/k8s
```

Watch it come up:

```bash
kubectl -n orderflow rollout status statefulset/postgres
kubectl -n orderflow wait --for=condition=complete job/orderflow-migrate --timeout=180s
kubectl -n orderflow rollout status deployment/orderflow-api
kubectl -n orderflow rollout status deployment/orderflow-web
```

Reach it either through the ingress:

```bash
echo "$(minikube ip) orderflow.local" | sudo tee -a /etc/hosts
# then open http://orderflow.local
```

or without touching `/etc/hosts`:

```bash
kubectl -n orderflow port-forward svc/orderflow-web 8080:80
# then open http://localhost:8080
```

### Deploying a change

**Bump the tag.** Do not rebuild `:v1` and expect the cluster to notice — with
`imagePullPolicy: IfNotPresent`, a node that already holds an image by that tag keeps
running the old one, silently. That is a real failure mode and it cost time while building
this.

```bash
docker tag orderflow-api:latest orderflow-api:v2
minikube image load orderflow-api:v2
kubectl -n orderflow set image deployment/orderflow-api api=orderflow-api:v2
```

### What the manifests contain

| File | Purpose |
|---|---|
| `namespace.yaml` | Everything is namespaced to `orderflow` |
| `configmap.yaml` | Non-secret settings |
| `secret.yaml` | Connection string, JWT signing key, seed credentials |
| `postgres/` | StatefulSet with a PersistentVolumeClaim, plus a headless Service |
| `api/migration-job.yaml` | Runs `--migrate-only` once before the Deployment |
| `api/` | Deployment (2 replicas) and ClusterIP Service |
| `web/` | Deployment (2 replicas) and ClusterIP Service |
| `ingress.yaml` | Single public entry point |

**Why a migration Job.** The API can migrate at startup, and does under compose. With two
replicas that becomes a race to apply the same schema, so Kubernetes runs the same image
with `--migrate-only` as a Job, and `Database__MigrateOnStartup=false` stops the pods
repeating the work.

**Why Postgres is a StatefulSet.** It needs a stable network identity and a volume that
survives rescheduling. A Deployment would hand a restarted pod a fresh volume.

---

## 3. AWS — the low-cost path (single EC2, k3s)

Suitable for a demo or a portfolio deployment. One instance, no managed control plane, no
NAT gateway.

**Roughly $10–15/month**, dominated by the instance. Prices are indicative and vary by
region and over time.

### Steps

1. **Launch one instance.** `t4g.small` (ARM, 2 GB) in a public subnet with a public IP.
   ARM is cheaper and the images build for `linux/arm64` on an Apple Silicon machine
   already. Security group: inbound 22 from your IP, 80 and 443 from anywhere.

2. **Install k3s** — a single binary with a conformant Kubernetes API:

   ```bash
   curl -sfL https://get.k3s.io | sh -
   sudo k3s kubectl get nodes
   ```

   k3s ships Traefik as its ingress controller. Either set
   `ingressClassName: traefik` in `ingress.yaml`, or install k3s with
   `--disable=traefik` and deploy ingress-nginx instead.

3. **Get the images onto the box.** Either push to ECR and pull from there, or for a
   single node, skip the registry entirely:

   ```bash
   docker save orderflow-api:v1 | sudo k3s ctr images import -
   ```

4. **Apply the manifests**, with real secrets:

   ```bash
   kubectl create secret generic orderflow-secrets -n orderflow \
     --from-literal=Jwt__SigningKey="$(openssl rand -base64 48)" \
     --from-literal=Seed__AdminPassword="$(openssl rand -base64 24)" \
     ...
   kubectl apply -k deploy/k8s
   ```

5. **TLS** with cert-manager and Let's Encrypt, or Cloudflare in front.

### Tradeoffs

One node means no failover, and Postgres in-cluster on an EBS volume means backups are
your job (`pg_dump` on a CronJob to S3, at minimum). That is an acceptable trade for a
demo and not for anything holding real orders.

---

## 4. AWS — the EKS path

What this would look like run properly.

**Roughly $150–250/month** before traffic. The control plane is about $73/month on its own,
and a NAT gateway is about $32/month plus data — often the surprise line item.

### Differences from the local manifests

| Concern | Local | EKS |
|---|---|---|
| Images | `minikube image load` | **ECR**, immutable tags, pulled via the node role |
| Database | Postgres StatefulSet | **RDS PostgreSQL 16**, Multi-AZ, automated backups |
| Ingress | ingress-nginx | **AWS Load Balancer Controller** → ALB |
| TLS | none | **ACM** certificate on the ALB |
| Secrets | committed placeholder | **Secrets Manager** via External Secrets Operator |
| Storage | minikube provisioner | **EBS CSI driver** (only if keeping Postgres in-cluster) |
| Scaling | fixed replicas | **HPA** on CPU, **Karpenter** or managed node groups |

### Outline

1. **Cluster** — `eksctl create cluster` or Terraform. Private subnets for nodes, public
   for the ALB.

2. **Drop the Postgres manifests** and point the connection string at RDS. In
   `deploy/k8s/kustomization.yaml`, remove `postgres/service.yaml` and
   `postgres/statefulset.yaml`.

3. **Secrets without committing them.** Store the connection string and JWT signing key in
   Secrets Manager and sync with External Secrets Operator. Note that a plain Kubernetes
   Secret is only base64-encoded, not encrypted, unless encryption at rest is enabled on
   etcd — which EKS does by default with a KMS key.

4. **IRSA, not node roles.** Give the service account an IAM role scoped to exactly the
   secret it reads, rather than granting the whole node group.

5. **Ingress becomes an ALB:**

   ```yaml
   metadata:
     annotations:
       kubernetes.io/ingress.class: alb
       alb.ingress.kubernetes.io/scheme: internet-facing
       alb.ingress.kubernetes.io/target-type: ip
       alb.ingress.kubernetes.io/certificate-arn: arn:aws:acm:...
       alb.ingress.kubernetes.io/ssl-redirect: "443"
   ```

6. **The migration Job still applies**, and matters more: with an HPA the replica count is
   not something you control by hand, so startup migration is not an option.

### What would need adding for production

- Structured logs shipped somewhere queryable (CloudWatch, or OpenTelemetry to a collector)
- Metrics and tracing — the service currently has health checks but no instrumentation
- `PodDisruptionBudget` so a node drain cannot take every replica at once
- `NetworkPolicy` restricting which pods may reach the database
- Rate limiting at the ingress
- Refresh tokens; one-hour access tokens with no refresh is thin for real users
