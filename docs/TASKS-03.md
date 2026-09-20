# FoodLoop — Tasks 3 of 3
## Functional Completion, Hardening & Demo Readiness

هذه آخر مرحلة وظيفية للفريق قبل أن يقوم براء بعمل **Final UI Redesign** مركزيًا. أحدث `develop` يحتوي على Stage 2 كاملة ومختبرة؛ Stage 3 تضيف فقط الوظائف الصغيرة الناقصة ذات القيمة العالية، ثم تقفل المشروع بالـhardening والاختبارات والتوثيق.

> مهم: لا تعملوا redesign عام. لا تغيّروا theme/navbar/typography/global visual system. المطلوب UI وظيفي وواضح وresponsive فقط. الـfinal look سيُعمل مركزيًا بعد هذه المرحلة.

## البداية والتسليم للجميع

1. ابدأوا من آخر `develop` فقط:
   ```powershell
   git checkout develop
   git pull --ff-only origin develop
   ```
2. لا تستخدموا فروع Stage 1 أو Stage 2 القديمة.
3. لا schema changes أو migrations أو enums جديدة إلا بعد مراجعة براء والـfeature owner المتأثر.
4. خارج النطاق: LLM، SignalR، full notification system، camera scanner، live GPS، advanced analytics، cancellation بعد courier assignment.
5. Automatic Donation Expiry **داخل Stage 3** وموزع بين Alaa (القواعد/use case) وبراء (scheduled execution/integration).
6. كل شاشة متغيرة تُجرّب على Desktop وعلى Mobile قريب من `390 × 844`.
7. UI لا يستبدل server authorization/ownership/state validation.
8. كل التوقيتات المعروضة بوضوح UTC.
9. ممنوع passwords/secrets/raw handover codes في Audit أو logs أو screenshots أو PR descriptions.
10. لا تغيّروا `_Layout.cshtml` أو global theme/CSS إلا لإصلاح functional/responsive bug وبعد تنسيق مع براء.
11. كل PR يحتوي:
    - Success scenario.
    - Rejected scenario.
    - Manual mobile check.
    - Tests added/updated.
    - `dotnet build FoodLoop.slnx -c Release`.
    - `dotnet test FoodLoop.slnx -c Release --verbosity minimal`.
    - Screenshots لأي شاشة تغيّرت.
12. Business-logic conflict لا يُحل باختيار نسخة ملف كاملة؛ يرجع للـfeature owner + براء.

---

## Jana — Auth Hardening + Organization Profile

**Branch:** `feature/jana-stage3-auth-profile`

### الجزء الجديد

1. **My Organization / Organization Profile** للمستخدم المرتبط بجهة:
   - يعرض Name, LicenseNumber, Type, Status, Address.
   - القراءة تظل حسب access rules الحالية.
   - تعديل الحقول الآمنة فقط **Name وAddress** مسموح إذا كانت Organization حالتها **Active**.
   - Suspended Beneficiary يمكنه العرض فقط ولا يمكنه تعديل الـprofile.
   - Pending/Rejected/Suspended organizations لا تنفذ profile mutation حتى عبر direct POST.
   - لا يسمح من هذه الصفحة بتغيير Type أو LicenseNumber أو Status أو Identity Role.
   - Admin/Courier الذين لا يملكون Organization يحصلون على سلوك واضح وليس NullReference/500.
   - أي update يستخدم server-side ownership + organization-status checks وRowVersion إن كانت الصفحة تعدل Organization tracked state.

2. بعد الحفظ يظهر التغيير بعد Refresh ويسجل Audit مناسب مثل `OrganizationUpdated` بنفس SaveChanges إن كانت عملية update فعلية.

### Hardening

3. دعم `ReturnUrl` في Login:
   - local URLs فقط.
   - external/scheme-relative URLs يتم تجاهلها.
   - fallback آمن عند عدم وجود ReturnUrl.

4. تحسين حالات الحساب:
   - Pending وRejected برسالة واضحة.
   - Suspended Donor ممنوع.
   - Suspended Beneficiary يستطيع login للـread-only history فقط.

5. Regression:
   - Active organization تستطيع تعديل Name/Address.
   - Suspended Beneficiary تستطيع عرض profile فقط ولا تستطيع تعديله.
   - direct POST من Suspended/Pending/Rejected organization مرفوض.
   - duplicate/invalid auth scenarios الحالية.
   - non-Admin لا يدخل organization management.
   - direct URL لا يكشف Organization أخرى.
   - password لا يعاد عرضه في HTML بعد registration/login failure.

### قبول التسليم

- مستخدم Organization يشاهد بيانات جهته فقط.
- **Only Active organizations** تستطيع تعديل Name/Address.
- Suspended Beneficiary تظل read-only حتى عبر direct POST.
- Foreign organization ID لا يعطي data disclosure.
- Admin/Courier بدون Organization لا يكسروا الصفحة.
- local ReturnUrl يعمل وexternal ReturnUrl لا يعمل.
- كل authorization على السيرفر.
- Mobile usable بدون redesign عام.

---

## Alaa — Donation Details + My Donations + Expiry Policy

**Branch:** `feature/alaa-stage3-donations-expiry`

### الجزء الجديد

1. **Donation Details**:
   - Donor يرى تفاصيل Donation التابعة لجهته.
   - Marketplace/Beneficiary لا يحصل على بيانات غير مسموحة خارج الـpublic/available flow.
   - تعرض title, category, quantity/unit, prepared/expiry UTC, pickup address, storage instructions, status.

2. **My Donations history/filtering**:
   - فلتر Status بسيط.
   - ترتيب ثابت.
   - pagination server-side إن كانت القائمة paginated حاليًا.
   - Draft/Available/Claimed/Closed/Expired واضحة وظيفيًا.

3. **Automatic Expiry application use case** — Alaa تملك القاعدة:
   - المرشحون فقط: `FoodDonation.Status == Available` و `ExpiresAtUtc <= TimeProvider.GetUtcNow()`.
   - الانتقال: `Available -> Expired`.
   - لا تغيّر Draft أو Claimed أو PickupPending/InTransit/Closed.
   - لكل Donation انتهت فعليًا: Audit واحد `DonationExpired`.
   - الحالة + Audit في نفس save/transaction boundary المناسب.
   - العملية idempotent: تشغيلها مرة ثانية لا ينشئ Audit إضافيًا.
   - تعتمد على repository/application service، وليس DbContext مباشرة من hosted worker.
   - تعامل controlled مع RowVersion race مع Claim/Publish؛ لا retry لنفس stale tracked entities.

### Hardening

4. Marketplace:
   - search/category قبل pagination.
   - filters محفوظة مع navigation.
   - expired أو donor suspended لا يظهر.
   - Next لا يؤدي لصفحة فارغة بلا داعٍ.

5. Validation/ownership:
   - Quantity > 0.
   - Expiry > PreparedAt.
   - Publish يتطلب future expiry.
   - foreign donor direct Edit/Details مرفوض.

### Tests المطلوبة

- Available قبل expiry لا تتغير.
- Available عند/بعد expiry تصبح Expired.
- تشغيل expiry مرتين => Audit واحد فقط.
- Draft/Claimed/Closed لا تتغير.
- Expired لا تظهر في Marketplace.
- race بين Claim وExpiry: لا ينجح المساران في إنتاج state غير متسقة.
- My Donations status filter + ownership.

### قبول التسليم

- يمكن إظهار Donation تتحول تلقائيًا منطقيًا من Available إلى Expired عبر use case.
- Audit واحد بالضبط.
- لا active claim يتم كسره بواسطة expiry.
- Donation Details/My Donations تعمل بعد Refresh وعلى Mobile.

---

## Safa — Claim Details + Timeline + Cancellation Hardening

**Branch:** `feature/safa-stage3-claim-details`

### الجزء الجديد

1. **Claim Details**:
   - Beneficiary يرى Claim التابعة لمؤسسته فقط.
   - يعرض Donation summary، status، created time، assigned courier إن وجد بالقدر المسموح، والحالة الحالية.
   - foreign/nonexistent ID يبقيان indistinguishable => NotFound.

2. **Claim Timeline**:
   - timeline read model من البيانات الموجودة بالفعل (Claim status/audit/handover evidence حسب المتاح).
   - لا schema جديدة ولا duplicate history table.
   - أحداث واضحة مثل Claimed/Cancelled/Assigned/Pickup/Delivery/Closed عندما توجد أدلتها.
   - UTC واضح.
   - لا raw QR tokens أو sensitive audit details.

### Hardening

3. My Claims:
   - Cancelled وClosed يظلان في History.
   - pagination/order ثابت.
   - Suspended Beneficiary read-only.
   - Mobile usable.

4. Cancel:
   - `CanCancel` يتحكم في الزر فقط، والسيرفر يعيد التحقق كاملًا.
   - confirmation + prevent accidental double submit.
   - repeated Cancel لا يضيف Audit.
   - assigned/PickupPending/InTransit/Closed لا تُلغى.

### Tests المطلوبة

- Claim Details own vs foreign/nonexistent.
- Timeline لا يكشف بيانات جهة أخرى.
- Cancelled claim تظهر بعد Refresh وفي timeline.
- Suspended Beneficiary تستطيع القراءة ولا تستطيع mutation.
- repeated cancel.
- Cancel vs Assign concurrency.

### قبول التسليم

- Claim Details وTimeline يعملان من البيانات الحالية بدون schema change.
- ownership لا يمكن تجاوزه بـdirect URL.
- timeline يطابق الأحداث المحفوظة فعلًا.
- cancellation regression كلها خضراء.

---

## Haneen — Courier Task Details + Progress + Handover Hardening

**Branch:** `feature/haneen-stage3-task-details`

### الجزء الجديد

1. **Courier Task Details**:
   - assigned Courier فقط يرى task.
   - يعرض Donation title، donor/beneficiary names بالقدر اللازم، pickup address، expiry UTC، current status، next required step.
   - Admin assignment screen يعرض نفس الـcontext الضروري قبل assignment.

2. **Progress / handover evidence**:
   - يعرض progress من Assigned/PickupPending إلى InTransit ثم Closed وفق الـimplemented lifecycle.
   - يظهر Pickup/Delivery HandoverRecord evidence/timestamps عند وجودها.
   - لا يعرض raw token بعد مغادرة issuance page.
   - Closed task تظهر Completed بلا actions تنفيذية.

### Hardening

3. Code issuance:
   - Pickup/Delivery واضح.
   - QR = نفس raw token.
   - actual server expiry UTC.
   - Copy feedback + fallback.
   - regeneration يبطل القديم.

4. Verification:
   - wrong/expired/reused/wrong-purpose/wrong-courier رسائل واضحة.
   - prevent accidental double submit.
   - delivery before pickup مرفوض.
   - rejected attempts لا تغير DB ولا تخلق Audit/Handover إضافي.

### Tests المطلوبة

- Task Details assigned courier vs other courier/direct URL.
- Expired/reused/regenerated token.
- Wrong courier + wrong purpose.
- Delivery before pickup.
- Closed task cannot execute again.
- Handover evidence visible after Refresh.

### قبول التسليم

- courier journey مفهومة وظيفيًا من Task Details حتى Closed.
- evidence بعد Refresh مطابق للـDB.
- لا token leakage.
- Mobile usable بدون visual redesign.

---

## Stage 3 implementation status

- Jana: Organization Profile/Auth hardening merged into `develop`.
- Alaa: Donation Details/My Donations/expiry application use case merged into `develop`.
- Safa: Claim Details/Timeline/cancellation hardening merged into `develop`.
- Haneen: Courier Task Details/evidence hardening merged into `develop`.
- Baraa release branch: scheduler orchestration, Admin impact summary, final regression/docs and release verification.

No Stage 3 schema, migration or enum change is required.

---

## Baraa — Expiry Scheduler + Basic Impact + Final Integration

**Branch:** `feature/baraa-stage3-release`

> لا يعيد كتابة Donation expiry rules؛ يستدعي الـapplication use case الذي تملكه Alaa.

### الجزء الجديد

1. **Automatic Expiry scheduling/orchestration**:
   - Hosted service/BackgroundService بسيط يستخدم scope جديد لكل run.
   - يستدعي expiry application service فقط.
   - interval قابل للـconfiguration، مع default مناسب للـdemo/development.
   - cancellation token/shutdown محترم.
   - exception في run واحدة تُسجل بشكل آمن ولا تقتل web app.
   - لا overlapping runs.
   - لا DbContext singleton أو scoped service محتفظ به داخل hosted service.
   - اختبارات application expiry تبقى deterministic باستخدام TimeProvider؛ لا تعتمد tests على الانتظار الحقيقي.

2. **Basic Impact / Admin summary** بدون schema جديد:
   - Closed operations count.
   - Cancelled claims count.
   - Expired donations count.
   - Current Available donations count.
   - لو عُرض quantity impact، يتم **group by Unit** ولا تجمع وحدات مختلفة.
   - Admin-only server authorization.
   - read-only queries فقط.

### Final hardening/integration

3. Security regression:
   - direct URL authorization.
   - cross-organization access.
   - antiforgery.
   - ReturnUrl open-redirect protection.
   - no secrets/raw QR in Audit/logs/localStorage.
   - role + organization state checks.

4. Fresh DB journey:
   - Register Donor + Beneficiary.
   - Admin approve.
   - Create/Edit/Publish.
   - Marketplace + Claim.
   - Cancel مرة ثم Claim جديد.
   - Assign Courier.
   - Pickup -> InTransit.
   - Delivery -> Closed.
   - Audit filters.
   - Impact counts.
   - Expiry demo على Donation Available قصيرة العمر أو TimeProvider-controlled test path.

5. Docs/release:
   - README.
   - SHARED-CONTRACTS بعد اكتمال التنفيذ.
   - TASKS-03.
   - Demo script.
   - Troubleshooting.
   - ERD + physical DB diagram ضمن deliverables.
   - Release build + full SQL-backed tests + GitHub Actions.
   - fresh database applies existing migrations then seed.
   - repo hygiene/security scan يدوي للـsecrets/generated DB/build artifacts.

### تنفيذ Baraa الحالي

- `DonationExpiryBackgroundService` يعمل serially ويستخدم fresh scope لكل run ويستدعي `IDonationExpiryService` فقط.
- interval configurable؛ default = 60 seconds، مع cancellation وsafe exception logging.
- Admin impact يعرض Current Available / Closed operations / Cancelled claims / Expired donations إضافة إلى Pending organizations.
- Demo / troubleshooting / ERD deliverables موثقة في `STAGE3-DEMO.md` و`TROUBLESHOOTING.md` و`ERD.md`.
- لا schema/migration/enum changes.

### قبول التسليم

- automatic expiry تعمل end-to-end ولا تنتج duplicate Audit.
- scheduler لا يحتوي business rules.
- impact summary صحيحة ومحمية Admin-only.
- الرحلة الكاملة من Registration إلى Closed تعمل.
- الأربع roles: Admin/Donor/Beneficiary/Courier تعمل بصلاحياتها.
- Release build/tests/CI كلها خضراء.
- لا blocker معروف قبل final UI redesign.

---

## ترتيب الاعتماد والدمج

يمكن للفريق البدء بالتوازي، لكن يوجد dependency واحد مهم:

1. Jana — Auth/Profile.
2. Safa — Claim Details/Timeline.
3. Haneen — Task Details/Handover.
4. **Alaa — Expiry application contract يجب مراجعته قبل جزء scheduler عند Baraa.**
5. Baraa — scheduler/impact/final integration بعد ثبات contracts.

عمليًا يمكن دمج Jana/Alaa/Safa/Haneen حسب جاهزية الـPRs، ثم Baraa أخيرًا. أي conflict في business logic يراجع مع الـowner.

## العرض لكل عضو

كل عضو يجهز 2–3 دقائق:

1. الوظيفة الجديدة في Stage 3.
2. Success scenario.
3. Rejected/edge scenario.
4. Refresh يثبت persistence.
5. Test يثبت السلوك.

## بعد Stage 3

بعد دمج Stage 3 وإغلاق الـfunctional scope، يقوم براء بمرحلة منفصلة للـ**Final UI Redesign**:
- visual system/theme.
- navbar/layout النهائي.
- typography/colors/spacing.
- page composition.
- presentation screenshots.

لا يُعاد فتح Business Logic أثناء الـredesign إلا لإصلاح bug حقيقي.
